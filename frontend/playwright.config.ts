import {defineConfig, devices} from '@playwright/test';

const baseURL = process.env.DEALERSYNC_E2E_BASE_URL;
if (!process.env.DEALERSYNC_E2E_RUN_ID || !baseURL ||
    !/^http:\/\/127\.0\.0\.1:\d+$/.test(baseURL)) {
    throw new Error('Run npm run test:e2e so the isolated environment is created first.');
}

export default defineConfig({
    testDir: './e2e',
    testMatch: '*.spec.ts',
    fullyParallel: false,
    workers: 1,
    retries: 0,
    forbidOnly: true,
    timeout: 30_000,
    reporter: [['list'], ['html', {open: 'never'}]],
    use: {
        baseURL,
        trace: 'retain-on-failure',
        screenshot: 'only-on-failure',
        serviceWorkers: 'block',
    },
    projects: [{name: 'chromium', use: {...devices['Desktop Chrome']}}],
});
