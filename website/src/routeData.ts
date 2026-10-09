import { defineRouteMiddleware } from '@astrojs/starlight/route-data';

const siteUrl = 'https://rodri-oliveira-dev.github.io/adr-guard/';
const repositoryUrl = 'https://github.com/rodri-oliveira-dev/adr-guard';
const imageUrl = `${siteUrl}social-preview-v2.png`;
const authorId = 'https://rodri-oliveira-dev.github.io/#person';

const sectionNames: Record<string, Record<string, string>> = {
  adoption: { en: 'Team adoption', 'pt-BR': 'Adoção em equipe' },
  examples: { en: 'ADR examples', 'pt-BR': 'Exemplos de ADR' },
  product: { en: 'Product', 'pt-BR': 'Produto' },
  reference: { en: 'Reference', 'pt-BR': 'Referência' }
};

type HeadTag = 'link' | 'style' | 'title' | 'base' | 'meta' | 'script' | 'noscript' | 'template';

function tag(tag: HeadTag, attrs: Record<string, string | boolean | undefined>, content?: string) {
  return { tag, attrs, content };
}

export const onRequest = defineRouteMiddleware((context) => {
  const route = context.locals.starlightRoute;
  const { title } = route.entry.data;
  const description = route.entry.data.description ?? title;
  const canonical = new URL(context.url.pathname, siteUrl).href;
  const isNotFound = route.id === '404' || route.id.endsWith('/404');
  const isHomepage = route.id === '' || route.id === 'index';
  const socialAlt = route.lang === 'pt-BR'
    ? 'ADR Guard — Registros de Decisões Arquiteturais'
    : 'ADR Guard — Architecture Decision Records';

  route.head.push(
    tag('meta', { name: 'robots', content: isNotFound ? 'noindex,follow' : 'index,follow,max-image-preview:large' }),
    tag('meta', { property: 'og:type', content: isHomepage ? 'website' : 'article' }),
    tag('meta', { property: 'og:image', content: imageUrl }),
    tag('meta', { property: 'og:image:type', content: 'image/png' }),
    tag('meta', { property: 'og:image:width', content: '1200' }),
    tag('meta', { property: 'og:image:height', content: '630' }),
    tag('meta', { property: 'og:image:alt', content: socialAlt }),
    tag('meta', { name: 'twitter:title', content: title }),
    tag('meta', { name: 'twitter:description', content: description }),
    tag('meta', { name: 'twitter:image', content: imageUrl }),
    tag('meta', { name: 'twitter:image:alt', content: socialAlt })
  );

  if (isNotFound) return;

  const graph: Record<string, unknown>[] = [
    {
      '@type': 'WebSite',
      '@id': `${siteUrl}#website`,
      name: 'ADR Guard documentation',
      url: siteUrl,
      description: 'Bilingual documentation for Architecture Decision Records and the ADR Guard open-source validation tool.',
      inLanguage: ['en', 'pt-BR']
    },
    {
      '@type': 'SoftwareApplication',
      '@id': `${siteUrl}#software`,
      name: 'ADR Guard',
      url: siteUrl,
      description: 'Open-source tooling to create, validate, review, and evolve Architecture Decision Records.',
      applicationCategory: 'DeveloperApplication',
      operatingSystem: ['Windows', 'macOS', 'Linux'],
      license: `${repositoryUrl}/blob/main/LICENSE`,
      sameAs: [repositoryUrl]
    }
  ];

  if (!isHomepage) {
    const pathWithoutLocale = route.locale ? route.id.replace(new RegExp(`^${route.locale}/?`), '') : route.id;
    const section = pathWithoutLocale.split('/')[0] ?? '';
    const sectionLabels = sectionNames[section];
    const breadcrumbItems: Record<string, unknown>[] = [
      { '@type': 'ListItem', position: 1, name: 'ADR Guard', item: route.lang === 'pt-BR' ? `${siteUrl}pt-br/` : siteUrl }
    ];
    if (sectionLabels && pathWithoutLocale !== section) {
      breadcrumbItems.push({
        '@type': 'ListItem',
        position: breadcrumbItems.length + 1,
        name: sectionLabels[route.lang] ?? sectionLabels.en,
        item: `${siteUrl}${route.locale ? `${route.locale}/` : ''}${section}/`
      });
    }
    breadcrumbItems.push({ '@type': 'ListItem', position: breadcrumbItems.length + 1, name: title, item: canonical });

    graph.push(
      {
        '@type': 'BreadcrumbList',
        '@id': `${canonical}#breadcrumb`,
        itemListElement: breadcrumbItems
      },
      {
        '@type': ['Article', 'TechArticle'],
        '@id': `${canonical}#article`,
        headline: title,
        description,
        url: canonical,
        inLanguage: route.lang,
        image: imageUrl,
        author: { '@type': 'Person', '@id': authorId, name: 'Rodrigo de Oliveira', url: 'https://rodri-oliveira-dev.github.io/' },
        isPartOf: { '@id': `${siteUrl}#website` },
        about: { '@id': `${siteUrl}#software` }
      }
    );
  }

  const structuredData = JSON.stringify({ '@context': 'https://schema.org', '@graph': graph }).replaceAll('<', '\\u003c');
  route.head.push(tag('script', { type: 'application/ld+json' }, structuredData));
});
