import {readFileSync, readdirSync} from 'node:fs';

const root = new URL('../', import.meta.url);
const decoder = new TextDecoder('utf-8', {fatal: true});
const webFields = value => value.match(/\{[A-Za-z_][A-Za-z_0-9]*\}/g) ?? [];
const desktopTokens = value => value.match(/\{\{|\}\}|\{\d+\}|[{}]/g) ?? [];
const desktopFields = value => desktopTokens(value).filter(token => token.length > 2);

function checkCatalogs(directory, placeholders) {
  const path = new URL(directory, root);
  const files = readdirSync(path).filter(file => file.endsWith('.json'));
  const tags = new Set();
  const readCatalog = file => {
    const contents = decoder.decode(readFileSync(new URL(file, path)));
    const catalog = JSON.parse(contents);
    if (!catalog || Array.isArray(catalog) || typeof catalog !== 'object' ||
        Object.values(catalog).some(value => typeof value !== 'string')) {
      throw new Error(`${directory}${file} must contain a flat JSON object with string values`);
    }
    const keyCount = [...contents.matchAll(/"(?:[^"\\]|\\.)*"/g)]
      .filter(match => /^\s*:/.test(contents.slice(match.index + match[0].length))).length;
    if (keyCount !== Object.keys(catalog).length) {
      throw new Error(`${directory}${file} contains duplicate keys`);
    }
    return catalog;
  };
  if (!files.includes('ru.json') || !files.includes('en.json')) {
    throw new Error(`${directory} must provide ru.json and en.json`);
  }
  const source = readCatalog('ru.json');
  const fields = value => JSON.stringify([...new Set(placeholders(value))].sort());
  for (const file of files) {
    const tag = file.slice(0, -5).replaceAll('_', '-').toLowerCase();
    if (tag === 'system' || tag.length > 128 || !Intl.getCanonicalLocales(tag).length) {
      throw new Error(`${directory}${file} must be named after a valid language tag`);
    }
    const canonical = tag === 'zh-hans' ? 'zh-cn' : tag;
    if (tags.has(canonical)) throw new Error(`${directory}${file} duplicates locale ${canonical}`);
    tags.add(canonical);
    const catalog = file === 'ru.json' ? source : readCatalog(file);
    for (const [key, value] of Object.entries(catalog)) {
      const location = `${directory}${file} ${JSON.stringify(key)}`;
      if (!Object.hasOwn(source, key)) throw new Error(`${location} is missing from ru.json`);
      if (file === 'ru.json' && !value.trim()) throw new Error(`${location} has an empty source value`);
      if (!value) continue;
      const reference = file === 'ru.json' ? key : source[key];
      if (fields(value) !== fields(reference)) throw new Error(`${location} changed placeholders`);
      if (placeholders === desktopFields && placeholders(reference).length &&
          desktopTokens(value).some(token => token.length === 1)) {
        throw new Error(`${location} contains unescaped braces in a formatted string`);
      }
    }
  }
  console.log(`${directory} validated ${files.length} catalogs, ${Object.keys(source).length} source strings`);
}

checkCatalogs('src/SideWidget.Web/src/locales/', webFields);
checkCatalogs('src/SideWidget.App/Assets/Locales/', desktopFields);
