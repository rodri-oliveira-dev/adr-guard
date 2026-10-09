import { defineConfig } from 'astro/config';
import sitemap from '@astrojs/sitemap';
import starlight from '@astrojs/starlight';

const repository = 'https://github.com/rodri-oliveira-dev/adr-guard';

export default defineConfig({
  site: 'https://rodri-oliveira-dev.github.io',
  base: '/adr-guard',
  trailingSlash: 'always',
  integrations: [
    sitemap({ i18n: { defaultLocale: 'en', locales: { en: 'en', 'pt-BR': 'pt-br' } } }),
    starlight({
      title: 'ADR Guard',
      description: 'Document, validate, review, and evolve architecture decisions with confidence.',
      logo: { src: './src/assets/logo.svg', replacesTitle: true },
      favicon: '/favicon.svg',
      social: [{ icon: 'github', label: 'GitHub', href: repository }],
      editLink: { baseUrl: `${repository}/edit/feat/documentation-portal/website/` },
      lastUpdated: true,
      defaultLocale: 'root',
      locales: {
        root: { label: 'English', lang: 'en' },
        'pt-br': { label: 'Português (Brasil)', lang: 'pt-BR' }
      },
      customCss: ['./src/styles/tokens.css', './src/styles/custom.css'],
      head: [
        { tag: 'meta', attrs: { property: 'og:type', content: 'website' } },
        { tag: 'meta', attrs: { property: 'og:image', content: 'https://rodri-oliveira-dev.github.io/adr-guard/social-preview.png' } },
        { tag: 'meta', attrs: { name: 'twitter:card', content: 'summary_large_image' } },
        { tag: 'meta', attrs: { name: 'twitter:image', content: 'https://rodri-oliveira-dev.github.io/adr-guard/social-preview.png' } },
        { tag: 'link', attrs: { rel: 'alternate', hreflang: 'x-default', href: 'https://rodri-oliveira-dev.github.io/adr-guard/' } }
      ],
      sidebar: [
        { label: 'Start here', translations: { 'pt-BR': 'Comece aqui' }, items: [
          { label: 'Overview', translations: { 'pt-BR': 'Visão geral' }, slug: 'index' },
          { label: 'Create your first ADR', translations: { 'pt-BR': 'Crie seu primeiro ADR' }, slug: 'getting-started' },
          { label: 'Choose a template', translations: { 'pt-BR': 'Escolha um template' }, slug: 'templates' }
        ]},
        { label: 'Learn ADRs', translations: { 'pt-BR': 'Aprenda sobre ADRs' }, items: [{ autogenerate: { directory: 'learn' } }] },
        { label: 'Adopt with your team', translations: { 'pt-BR': 'Adote com sua equipe' }, items: [{ autogenerate: { directory: 'adoption' } }] },
        { label: 'ADR examples', translations: { 'pt-BR': 'Exemplos de ADR' }, items: [{ autogenerate: { directory: 'examples' } }] },
        { label: 'Product', translations: { 'pt-BR': 'Produto' }, items: [{ autogenerate: { directory: 'product' } }] },
        { label: 'Reference', translations: { 'pt-BR': 'Referência' }, items: [{ autogenerate: { directory: 'reference' } }] }
      ]
    })
  ],
  markdown: { shikiConfig: { themes: { light: 'github-light', dark: 'github-dark' } } },
  vite: { build: { cssMinify: 'lightningcss' } }
});
