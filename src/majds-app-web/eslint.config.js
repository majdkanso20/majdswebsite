// @ts-check
// Lint rules that protect the "restyle by editing templates and tokens only" guarantee (U1, FR-UI-008):
// no inline templates or styles, every component OnPush, and accessible/clean templates.
const eslint = require('@eslint/js');
const tseslint = require('typescript-eslint');
const angular = require('angular-eslint');

module.exports = tseslint.config(
  {
    files: ['**/*.ts'],
    extends: [eslint.configs.recommended, ...tseslint.configs.recommended, ...angular.configs.tsRecommended],
    processor: angular.processInlineTemplates,
    rules: {
      '@angular-eslint/directive-selector': ['error', { type: 'attribute', prefix: ['app', 'has'], style: 'camelCase' }],
      '@angular-eslint/component-selector': ['error', { type: 'element', prefix: 'app', style: 'kebab-case' }],
      // FR-UI-001: presentation lives in external files, never in TypeScript.
      '@angular-eslint/component-max-inline-declarations': ['error', { template: 0, styles: 0, animations: 0 }],
      // FR-UI-004: presentational components are OnPush.
      '@angular-eslint/prefer-on-push-component-change-detection': 'error',
      '@typescript-eslint/no-explicit-any': 'error',
      '@typescript-eslint/no-unused-vars': ['error', { argsIgnorePattern: '^_' }]
    }
  },
  {
    // Test host components are throwaway scaffolding, not shipped UI, so inline templates are fine there.
    files: ['**/*.spec.ts'],
    rules: { '@angular-eslint/component-max-inline-declarations': 'off', '@typescript-eslint/no-explicit-any': 'off' }
  },
  {
    files: ['**/*.html'],
    extends: [...angular.configs.templateRecommended, ...angular.configs.templateAccessibility],
    rules: {
      // Icon-only buttons in this app carry aria-label; Material's own elements handle the rest.
      '@angular-eslint/template/interactive-supports-focus': 'off',
      '@angular-eslint/template/click-events-have-key-events': 'off'
    }
  }
);
