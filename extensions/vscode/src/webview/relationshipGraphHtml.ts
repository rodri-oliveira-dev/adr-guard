export function relationshipGraphWebviewHtml(
  cspSource: string,
  scriptUri: string,
  styleUri: string,
  language: string,
  nonce: string,
): string {
  return `<!DOCTYPE html><html lang="${escapeAttribute(language)}"><head><meta charset="UTF-8"><meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src 'none'; font-src 'none'; connect-src 'none'; style-src ${escapeAttribute(cspSource)}; script-src 'nonce-${escapeAttribute(nonce)}';"><meta name="viewport" content="width=device-width, initial-scale=1.0"><link rel="stylesheet" href="${escapeAttribute(styleUri)}"><title>ADR Guard</title></head><body><div id="graph-status" class="screen-reader-only" role="status" aria-live="polite" aria-atomic="true"></div><main id="app"></main><script nonce="${escapeAttribute(nonce)}" src="${escapeAttribute(scriptUri)}"></script></body></html>`;
}

function escapeAttribute(value: string): string {
  return value.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;').replaceAll("'", '&#39;');
}
