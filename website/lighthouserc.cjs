module.exports = {
  ci: {
    collect: {
      url: [
        'http://127.0.0.1:4321/adr-guard/',
        'http://127.0.0.1:4321/adr-guard/learn/what-is-an-adr/',
        'http://127.0.0.1:4321/adr-guard/getting-started/',
        'http://127.0.0.1:4321/adr-guard/examples/',
        'http://127.0.0.1:4321/adr-guard/adoption/',
        'http://127.0.0.1:4321/adr-guard/reference/',
        'http://127.0.0.1:4321/adr-guard/pt-br/',
        'http://127.0.0.1:4321/adr-guard/pt-br/learn/what-is-an-adr/',
        'http://127.0.0.1:4321/adr-guard/pt-br/getting-started/',
        'http://127.0.0.1:4321/adr-guard/pt-br/examples/',
        'http://127.0.0.1:4321/adr-guard/pt-br/adoption/',
        'http://127.0.0.1:4321/adr-guard/pt-br/reference/'
      ],
      numberOfRuns: 3,
      settings: { chromeFlags: '--no-sandbox --disable-dev-shm-usage' }
    },
    assert: {
      assertions: {
        'categories:performance': ['warn', { minScore: 0.85, aggregationMethod: 'median' }],
        'categories:accessibility': ['error', { minScore: 0.95, aggregationMethod: 'median' }],
        'categories:best-practices': ['error', { minScore: 0.95, aggregationMethod: 'median' }],
        'categories:seo': ['error', { minScore: 0.95, aggregationMethod: 'median' }]
      }
    }
  }
};
