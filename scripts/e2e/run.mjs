import assert from 'node:assert/strict';
import {spawn} from 'node:child_process';
import {randomBytes, randomUUID} from 'node:crypto';
import {mkdtemp, rm} from 'node:fs/promises';
import {createServer} from 'node:net';
import {tmpdir} from 'node:os';
import {dirname, join, resolve} from 'node:path';
import {setTimeout as delay} from 'node:timers/promises';
import {fileURLToPath} from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const frontend = join(root, 'frontend');
const runId = randomUUID();
const containerName = `dealersync-e2e-${runId}`;
const database = `dealersync_e2e_${runId.replaceAll('-', '')}`;
const username = 'dealersync_e2e';
const password = randomBytes(24).toString('hex');
const children = [];
const reservedPorts = [];
let containerId;
let containerCreationAttempted = false;
let temporaryDirectory;
let interrupted = 0;

// Do not inherit development connection strings, ASP.NET launch settings, VITE_*
// values, or NODE_OPTIONS (which could preload an environment-file loader).
const environment = Object.fromEntries(
    ['PATH', 'HOME', 'TMPDIR', 'TEMP', 'TMP', 'SystemRoot', 'DOTNET_ROOT',
        'DOTNET_CLI_HOME', 'NUGET_PACKAGES', 'DOCKER_HOST', 'DOCKER_CONTEXT',
        'DOCKER_CONFIG', 'CI', 'TERM', 'LANG'].flatMap((key) =>
        process.env[key] === undefined ? [] : [[key, process.env[key]]]),
);
Object.assign(environment, {
    DOTNET_ENVIRONMENT: 'Testing',
    ASPNETCORE_ENVIRONMENT: 'Testing',
    DOTNET_CLI_TELEMETRY_OPTOUT: '1',
    DOTNET_NOLOGO: '1',
});

function start(command, args, {cwd = root, env = environment, capture = false} = {}) {
    if (interrupted) throw new Error('Test run interrupted.');
    const child = spawn(command, args, {
        cwd, env, detached: true,
        stdio: capture ? ['ignore', 'pipe', 'inherit'] : 'inherit',
    });
    let stdout = '';
    if (capture) child.stdout.setEncoding('utf8').on('data', (chunk) => { stdout += chunk; });
    const entry = {child, finished: false};
    entry.done = new Promise((resolveDone) => {
        child.once('error', (error) => {
            entry.finished = true;
            resolveDone({code: 1, error, stdout});
        });
        child.once('close', (code, signal) => {
            entry.finished = true;
            resolveDone({code, signal, stdout});
        });
    });
    children.push(entry);
    return entry;
}

async function run(command, args, options) {
    const result = await start(command, args, options).done;
    if (result.error) throw result.error;
    if (result.code !== 0) throw new Error(`${command} exited unsuccessfully (${result.code ?? result.signal}).`);
    return result.stdout.trim();
}

function signalGroup(entry, signal) {
    if (!entry.child.pid) return;
    try { process.kill(-entry.child.pid, signal); }
    catch (error) { if (error.code !== 'ESRCH') throw error; }
}

for (const [signal, code] of [['SIGINT', 130], ['SIGTERM', 143]]) {
    process.on(signal, () => {
        interrupted = code;
        // Let the normal finally block clean up after the active command exits.
        for (const entry of children) signalGroup(entry, 'SIGTERM');
    });
}

async function reservePort() {
    const server = createServer();
    await new Promise((resolveListen, reject) => {
        server.once('error', reject);
        server.listen(0, '127.0.0.1', resolveListen);
    });
    const reservation = {
        port: server.address().port,
        release: () => server.listening
            ? new Promise((resolveClose) => server.close(resolveClose))
            : Promise.resolve(),
    };
    reservedPorts.push(reservation);
    return reservation;
}

async function waitUntil(check, description, service) {
    const deadline = Date.now() + 60_000;
    while (Date.now() < deadline) {
        if (interrupted) throw new Error('Test run interrupted.');
        if (service?.finished) throw new Error(`${description}: process exited before readiness.`);
        try { if (await check()) return; }
        catch { /* Retry readiness checks; do not retry a browser assertion. */ }
        await delay(200);
    }
    throw new Error(`Timed out waiting for ${description}.`);
}

async function inspectOwnedContainer() {
    const [container] = JSON.parse(await run('docker', ['inspect', containerName], {capture: true}));
    assert.equal(container.Name, `/${containerName}`);
    assert.equal(container.Config.Labels['com.dealersync.e2e.run'], runId);
    if (containerId) assert.equal(container.Id, containerId);
    assert(container.Config.Env.includes(`POSTGRES_DB=${database}`));
    assert(container.Config.Env.includes(`POSTGRES_USER=${username}`));
    assert(container.Mounts.some((mount) => mount.Type === 'tmpfs' && mount.Destination === '/var/lib/postgresql/data'));
    assert(container.Mounts.every((mount) => mount.Type === 'tmpfs'));
    return container;
}

async function cleanup() {
    for (const reservation of reservedPorts) await reservation.release();
    // Stop our process groups before removing the database they were using.
    for (const entry of [...children].reverse()) {
        signalGroup(entry, 'SIGTERM');
        if (!entry.finished) await Promise.race([entry.done, delay(5_000, undefined, {ref: false})]);
        signalGroup(entry, 'SIGKILL');
        await entry.done;
    }
    // Signal handling must not prevent the cleanup commands themselves from running.
    const exitSignal = interrupted;
    interrupted = 0;
    try {
        if (!containerId && containerCreationAttempted) {
            // Docker may finish creating the container just before an interruption.
            const found = await run('docker', [
                'ps', '--all', '--quiet', '--no-trunc',
                '--filter', `name=^/${containerName}$`,
                '--filter', `label=com.dealersync.e2e.run=${runId}`,
            ], {capture: true});
            if (found) containerId = found;
        }
        if (containerId) {
            const owned = await inspectOwnedContainer();
            await run('docker', ['rm', '--force', owned.Id], {capture: true});
            console.log('[e2e] Removed the owned PostgreSQL container; no volumes were removed.');
        }
    } finally {
        interrupted = exitSignal;
        if (temporaryDirectory) await rm(temporaryDirectory, {recursive: true, force: true});
    }
}

try {
    if (process.platform === 'win32') throw new Error('This first runner supports macOS and Linux process groups.');
    if (process.argv.slice(2).some((arg) => arg !== '--headed')) throw new Error('Only --headed is supported.');
    temporaryDirectory = await mkdtemp(join(tmpdir(), 'dealersync-e2e-'));
    console.log(`[e2e] Creating ${containerName} (PostgreSQL 17, temporary memory-backed storage).`);
    containerCreationAttempted = true;
    containerId = await run('docker', [
        'create', '--name', containerName, '--label', `com.dealersync.e2e.run=${runId}`,
        '--mount', 'type=tmpfs,destination=/var/lib/postgresql/data',
        '--publish', '127.0.0.1::5432',
        '--env', `POSTGRES_DB=${database}`, '--env', `POSTGRES_USER=${username}`,
        '--env', `POSTGRES_PASSWORD=${password}`, 'postgres:17',
    ], {capture: true});
    await inspectOwnedContainer();
    await run('docker', ['start', containerId], {capture: true});
    await waitUntil(async () => {
        const result = await start('docker', [
            'exec', containerId, 'pg_isready', '-h', '127.0.0.1', '-U', username, '-d', database,
        ], {capture: true}).done;
        return result.code === 0;
    }, 'PostgreSQL');
    const container = await inspectOwnedContainer();
    const [binding] = container.NetworkSettings.Ports['5432/tcp'];
    assert.equal(binding.HostIp, '127.0.0.1');
    const connection = `Host=127.0.0.1;Port=${Number(binding.HostPort)};Database=${database};Username=${username};Password=${password}`;
    // Verify the live database identity before migrations. No reset/drop SQL is used.
    const actualDatabase = await run('docker', [
        'exec', containerId, 'psql', '-U', username, '-d', database,
        '-Atc', 'SELECT current_database();',
    ], {capture: true});
    assert.equal(actualDatabase, database);
    const apiEnvironment = {
        ...environment, ConnectionStrings__DefaultConnection: connection,
        Logging__LogLevel__Default: 'Warning',
    };
    console.log('[e2e] Building the API and applying existing migrations to the verified test database.');
    await run('dotnet', ['build', 'backend/DealerSync.Api', '--verbosity', 'quiet'], {env: apiEnvironment});
    await run('dotnet', [
        'ef', 'database', 'update', '--no-build',
        '--project', 'backend/DealerSync.Infrastructure',
        '--startup-project', 'backend/DealerSync.Api', '--connection', connection,
    ], {env: apiEnvironment});

    const apiPort = await reservePort();
    const frontendPort = await reservePort();
    const apiUrl = `http://127.0.0.1:${apiPort.port}`;
    const baseUrl = `http://127.0.0.1:${frontendPort.port}`;
    await apiPort.release();
    const api = start('dotnet', [
        join(root, 'backend/DealerSync.Api/bin/Debug/net10.0/DealerSync.Api.dll'),
        '--urls', apiUrl,
    ], {cwd: temporaryDirectory, env: apiEnvironment});
    await waitUntil(async () => {
        const response = await fetch(`${apiUrl}/api/reconciliation/history`, {
            redirect: 'error', signal: AbortSignal.timeout(2_000),
        });
        return response.ok && JSON.stringify(await response.json()) === '[]';
    }, 'the API and its empty test database', api);

    const browserEnvironment = {
        ...environment, DEALERSYNC_E2E_RUN_ID: runId,
        DEALERSYNC_E2E_API_URL: apiUrl, DEALERSYNC_E2E_BASE_URL: baseUrl,
    };
    await frontendPort.release();
    const vite = start(process.execPath, [
        'node_modules/vite/bin/vite.js', '--config', 'e2e/vite.config.ts', '--mode', 'test',
    ], {cwd: frontend, env: browserEnvironment});
    await waitUntil(async () => {
        const response = await fetch(`${baseUrl}/api/reconciliation/history`, {
            redirect: 'error', signal: AbortSignal.timeout(2_000),
        });
        return response.ok && JSON.stringify(await response.json()) === '[]';
    }, 'the frontend proxy and test API', vite);
    console.log('[e2e] Running one Chromium reconciliation test against the isolated stack.');
    await run(process.execPath, [
        'node_modules/@playwright/test/cli.js', 'test', ...process.argv.slice(2),
    ], {cwd: frontend, env: browserEnvironment});
} catch (error) {
    console.error(`[e2e] ${error.message}`);
    process.exitCode = interrupted || 1;
} finally {
    try { await cleanup(); }
    catch (error) {
        console.error(`[e2e] Cleanup failed: ${error.message}. Owned container: ${containerName}`);
        process.exitCode = 1;
    }
    if (interrupted) process.exitCode = interrupted;
}
