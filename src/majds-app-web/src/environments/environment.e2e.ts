// Used only by the browser end-to-end tests (`npm run e2e`): the same app pointed at the throwaway API the tests start on its own port.
export const environment = {
  production: false,
  apiBaseUrl: 'http://localhost:5299/api'
};
