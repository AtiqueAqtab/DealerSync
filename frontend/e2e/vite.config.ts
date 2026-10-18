import {defineConfig, mergeConfig} from 'vite';
import applicationConfig from '../vite.config.ts';

const api = process.env.DEALERSYNC_E2E_API_URL;
const frontend = process.env.DEALERSYNC_E2E_BASE_URL;
if (!process.env.DEALERSYNC_E2E_RUN_ID || !api || !frontend ||
    ![api, frontend].every((url) => /^http:\/\/127\.0\.0\.1:\d+$/.test(url))) {
    throw new Error('The E2E Vite configuration requires the isolated test runner.');
}

export default mergeConfig(applicationConfig, defineConfig({
    envDir: false,
    server: {
        host: '127.0.0.1',
        port: Number(new URL(frontend).port),
        strictPort: true,
        proxy: {'/api': {target: api, changeOrigin: true}},
    },
}));
