import js from '@eslint/js';
import globals from 'globals';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';
import tseslint from 'typescript-eslint';
import { globalIgnores } from 'eslint/config';
import eslintConfigPrettier from 'eslint-config-prettier';

// Config plano de ESLint. `eslintConfigPrettier` va al final para desactivar
// reglas de formato que puedan chocar con Prettier.
export default tseslint.config(
  globalIgnores(['dist', 'coverage']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [js.configs.recommended, tseslint.configs.recommended, reactRefresh.configs.vite],
    plugins: {
      'react-hooks': reactHooks,
    },
    rules: {
      // `configs['recommended-latest']` de esta versión del plugin usa el
      // formato legacy de eslintrc (plugins como array de strings), que no
      // es compatible con flat config; reutilizamos solo sus reglas.
      ...reactHooks.configs['recommended-latest'].rules,
    },
    languageOptions: {
      ecmaVersion: 2020,
      globals: globals.browser,
    },
  },
  eslintConfigPrettier,
);
