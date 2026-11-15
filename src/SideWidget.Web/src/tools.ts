import {t} from './i18n';
import QRCode from 'qrcode';

function finite(value: number): number {
  if (!Number.isFinite(value)) throw new Error(t('Результат выходит за допустимый диапазон.'));
  return value;
}

/**
 * Arithmetic grammar: sum -> product -> unary -> postfix -> number/parentheses.
 * Percent is always division by 100, including in sums: 200 + 10% = 200.1.
 * Dot/comma are decimal separators, never grouping separators. Scientific
 * notation permits pasting the calculator's own output. No implicit multiply.
 * IEEE-754 binary64 operations; only final output rounds to 15 significant digits.
 * This is approximate arithmetic, not arbitrary precision or financial math.
 */
export function evaluate(expression: string): string {
  if (expression.length > 4096) throw new Error(t('Выражение слишком длинное.'));
  expression=expression.replace(/×/g,'*').replace(/÷/g,'/').replace(/−/g,'-');
  const tokens: (number | string)[] = [];
  let offset = 0;
  while (offset < expression.length) {
    const rest = expression.slice(offset);
    const whitespace = /^\s+/.exec(rest);
    if (whitespace) { offset += whitespace[0].length; continue; }
    const number = /^(?:\d+(?:[.,]\d*)?|[.,]\d+)(?:[eE][+-]?\d+)?/.exec(rest);
    if (number) {
      tokens.push(finite(Number(number[0].replace(',', '.'))));
      offset += number[0].length;
    } else if ('+-*/()%'.includes(rest[0])) {
      tokens.push(rest[0]);
      offset++;
    } else {
      throw new Error(t('Допустимы числа, скобки и операции + − * / %.'));
    }
    if (tokens.length > 512) throw new Error(t('В выражении слишком много операций.'));
  }
  let cursor = 0;
  const take = (token: string) => tokens[cursor] === token && (++cursor > 0);
  function primary(depth: number): number {
    if (depth > 32) throw new Error(t('Слишком много вложенных скобок или знаков.'));
    let value: number;
    if (take('(')) {
      value = sum(depth + 1);
      if (!take(')')) throw new Error(t('Закройте скобку.'));
    } else {
      const token = tokens[cursor++];
      if (typeof token !== 'number') throw new Error(t('Ожидается число или выражение в скобках.'));
      value = token;
    }
    while (take('%')) value /= 100;
    return value;
  }
  function unary(depth: number): number {
    if (depth > 32) throw new Error(t('Слишком много вложенных скобок или знаков.'));
    if (take('+')) return unary(depth + 1);
    if (take('-')) return -unary(depth + 1);
    return primary(depth);
  }
  function product(depth: number): number {
    let value = unary(depth);
    while (tokens[cursor] === '*' || tokens[cursor] === '/') {
      const operation = tokens[cursor++];
      const right = unary(depth);
      if (operation === '/' && right === 0) throw new Error(t('На ноль делить нельзя.'));
      value = finite(operation === '*' ? value * right : value / right);
    }
    return value;
  }
  function sum(depth: number): number {
    let value = product(depth);
    while (tokens[cursor] === '+' || tokens[cursor] === '-') {
      const operation = tokens[cursor++];
      const right = product(depth);
      value = finite(operation === '+' ? value + right : value - right);
    }
    return value;
  }
  const value = finite(sum(0));
  if (cursor !== tokens.length) throw new Error(t('Между числами должна быть операция.'));
  return String(finite(Number(value.toPrecision(15))));
}

type Unit = { id: string; name: string; scale: number };
type Category = { id: string; name: string; units: Unit[] };
const unit = (id: string, name: string, scale: number): Unit => ({id, name, scale});
const categories: Category[] = [
  {id: 'length', name: 'Длина', units: [
    unit('m', 'Метры (м)', 1), unit('cm', 'Сантиметры (см)', 0.01), unit('mm', 'Миллиметры (мм)', 0.001),
    unit('km', 'Километры (км)', 1000), unit('in', 'Дюймы', 0.0254), unit('ft', 'Футы', 0.3048),
    unit('yd', 'Ярды', 0.9144), unit('mi', 'Мили', 1609.344),
  ]},
  {id: 'area', name: 'Площадь', units: [
    unit('m2', 'Квадратные метры (м²)', 1), unit('cm2', 'Квадратные сантиметры (см²)', 0.0001),
    unit('km2', 'Квадратные километры (км²)', 1e6), unit('ha', 'Гектары (га)', 10000), unit('acre', 'Акры', 4046.8564224),
  ]},
  {id: 'volume', name: 'Объём', units: [
    unit('l', 'Литры (л)', 1), unit('ml', 'Миллилитры (мл)', 0.001),
    unit('m3', 'Кубические метры (м³)', 1000), unit('cm3', 'Кубические сантиметры (см³)', 0.001),
  ]},
  {id: 'mass', name: 'Масса', units: [
    unit('kg', 'Килограммы (кг)', 1), unit('g', 'Граммы (г)', 0.001), unit('mg', 'Миллиграммы (мг)', 1e-6),
    unit('t', 'Тонны (т)', 1000), unit('lb', 'Фунты', 0.45359237), unit('oz', 'Унции', 0.028349523125),
  ]},
  {id: 'temperature', name: 'Температура', units: [
    unit('C', 'Цельсий (°C)', 1), unit('F', 'Фаренгейт (°F)', 1), unit('K', 'Кельвины (K)', 1),
  ]},
  {id: 'speed', name: 'Скорость', units: [
    unit('ms', 'Метры в секунду (м/с)', 1), unit('kmh', 'Километры в час (км/ч)', 1 / 3.6),
    unit('mph', 'Мили в час', 0.44704), unit('knot', 'Узлы', 1852 / 3600),
  ]},
  {id: 'time', name: 'Время', units: [
    unit('s', 'Секунды', 1), unit('min', 'Минуты', 60), unit('h', 'Часы', 3600),
    unit('day', 'Сутки', 86400), unit('week', 'Недели', 604800),
  ]},
  {id: 'data', name: 'Размер данных', units: [
    unit('bit', 'Биты (bit)', 0.125), unit('B', 'Байты (B)', 1), unit('kB', 'Килобайты (kB, 1000 B)', 1e3),
    unit('MB', 'Мегабайты (MB, 1000² B)', 1e6), unit('GB', 'Гигабайты (GB, 1000³ B)', 1e9),
    unit('TB', 'Терабайты (TB, 1000⁴ B)', 1e12), unit('KiB', 'Кибибайты (KiB, 1024 B)', 1024),
    unit('MiB', 'Мебибайты (MiB, 1024² B)', 1024 ** 2), unit('GiB', 'Гибибайты (GiB, 1024³ B)', 1024 ** 3),
    unit('TiB', 'Тебибайты (TiB, 1024⁴ B)', 1024 ** 4),
  ]},
];

export const unitCategories = categories.map(({id, name, units}) => ({id, name, units: units.map(({id, name}) => ({id, name}))}));
const unitsById = new Map(categories.flatMap(category => category.units.map(unit => [unit.id, {...unit, category: category.id}] as const)));

/** Returns unrounded binary64 result; UI chooses display precision. */
export function convertUnit(value: number, from: string, to: string): number {
  finite(value);
  const source = unitsById.get(from);
  const target = unitsById.get(to);
  if (!source || !target || source.category !== target.category) throw new Error(t('Выберите единицы одной категории.'));
  if (source.category === 'temperature') {
    const minimum = from === 'C' ? -273.15 : from === 'F' ? -459.67 : 0;
    if (value < minimum) throw new Error(t('Температура не может быть ниже абсолютного нуля.'));
    if (from === to) return value;
    const celsius = from === 'K' ? value - 273.15 : from === 'F' ? (value - 32) * 5 / 9 : value;
    return finite(to === 'K' ? Math.max(0, celsius + 273.15) : to === 'F' ? celsius * 9 / 5 + 32 : celsius);
  }
  return finite(value * (source.scale / target.scale));
}

/** No alpha channel. RGB rounds HSL to nearest 8-bit channel; HSL output to 0.1. */
export function parseColor(text: string): {hex: string; rgb: string; hsl: string} {
  if (text.length > 128) throw new Error(t('Слишком длинное значение цвета.'));
  const input = text.trim();
  const hex = /^#?([\da-f]{3}|[\da-f]{6})$/i.exec(input);
  const rgb = /^rgb\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)$/i.exec(input);
  const decimal = '(\\d+(?:\\.\\d+)?|\\.\\d+)';
  const hsl = new RegExp(`^hsl\\(\\s*${decimal}\\s*,\\s*${decimal}%\\s*,\\s*${decimal}%\\s*\\)$`, 'i').exec(input);
  let channels: number[];
  if (hex) {
    const full = hex[1].length === 3 ? [...hex[1]].map(char => char + char).join('') : hex[1];
    channels = [0, 2, 4].map(index => parseInt(full.slice(index, index + 2), 16));
  } else if (rgb) {
    channels = rgb.slice(1).map(Number);
    if (channels.some(value => value > 255)) throw new Error(t('Каналы RGB — целые числа от 0 до 255.'));
  } else if (hsl) {
    const [hue, saturation, lightness] = hsl.slice(1).map(Number);
    if (hue > 360 || saturation > 100 || lightness > 100) throw new Error(t('H: 0–360; S и L: 0–100%.'));
    const h = hue / 60;
    const l = lightness / 100;
    const chroma = (1 - Math.abs(2 * l - 1)) * saturation / 100;
    const x = chroma * (1 - Math.abs(h % 2 - 1));
    const m = l - chroma / 2;
    const sectors = [[chroma,x,0], [x,chroma,0], [0,chroma,x], [0,x,chroma], [x,0,chroma], [chroma,0,x]];
    channels = sectors[Math.floor(h) % 6].map(value => Math.round((value + m) * 255));
  } else {
    throw new Error(t('Введите HEX, rgb(0, 0, 0) или hsl(0, 0%, 0%). Альфа не поддерживается.'));
  }
  const [r, g, b] = channels.map(value => value / 255);
  const max = Math.max(r, g, b), min = Math.min(r, g, b), delta = max - min;
  const lightness = (max + min) / 2;
  const saturation = delta === 0 ? 0 : delta / (1 - Math.abs(2 * lightness - 1));
  let hue = delta === 0 ? 0 : max === r ? ((g - b) / delta) % 6 : max === g ? (b - r) / delta + 2 : (r - g) / delta + 4;
  hue = (hue * 60 + 360) % 360;
  const rounded = (value: number) => Math.round(value * 10) / 10;
  return {
    hex: '#' + channels.map(value => value.toString(16).padStart(2, '0')).join('').toUpperCase(),
    rgb: `rgb(${channels.join(', ')})`,
    hsl: `hsl(${rounded(hue) % 360}, ${rounded(saturation * 100)}%, ${rounded(lightness * 100)}%)`,
  };
}

/**
 * Local encoding, no network. SVG is XML, PNG/JPEG are data URLs.
 * Integer module pitch, opaque white background, at least four quiet modules.
 * Requested size is exact; reject it if fewer than two pixels/module would fit.
 */
export async function makeQr(text: string, options: {size?: number; level?: 'L'|'M'|'Q'|'H'} = {}): Promise<{svg: string; png: string; jpeg: string}> {
  const size = options.size ?? 512;
  const level = options.level ?? 'M';
  if (!Number.isInteger(size) || size < 128 || size > 2048) throw new Error(t('Размер QR — целое число от 128 до 2048 пикселей.'));
  if (!['L', 'M', 'Q', 'H'].includes(level)) throw new Error(t('Неизвестный уровень коррекции QR.'));
  if (text.length === 0) throw new Error(t('Введите текст для QR.'));
  if (text.length > 6000 || new TextEncoder().encode(text).length > 2953) throw new Error(t('Текст слишком длинный для одного QR. Сократите его или разделите на части.'));
  // Reject malformed surrogate pairs: TextEncoder would silently replace them.
  if (/[\uD800-\uDBFF](?![\uDC00-\uDFFF])|(?<![\uD800-\uDBFF])[\uDC00-\uDFFF]/u.test(text)) throw new Error(t('Текст содержит неполный символ Unicode.'));
  let modules: ReturnType<typeof QRCode.create>['modules'];
  try {
    modules = QRCode.create(text, {errorCorrectionLevel: level}).modules;
  } catch {
    throw new Error(t('Текст не помещается в QR с выбранной коррекцией. Сократите текст или выберите другой уровень.'));
  }
  const scale = Math.floor(size / (modules.size + 8));
  if (scale < 2) throw new Error(t('Для такого текста увеличьте размер QR.'));
  const inset = Math.floor((size - modules.size * scale) / 2);
  const canvas = document.createElement('canvas');
  canvas.width = size;
  canvas.height = size;
  const context = canvas.getContext('2d');
  if (!context) throw new Error(t('Не удалось создать изображение QR.'));
  context.imageSmoothingEnabled = false;
  context.fillStyle = '#FFFFFF';
  context.fillRect(0, 0, size, size);
  context.fillStyle = '#000000';
  const paths: string[] = [];
  for (let row = 0; row < modules.size; row++) {
    for (let col = 0; col < modules.size; col++) {
      if (!modules.get(row, col)) continue;
      const x = inset + col * scale, y = inset + row * scale;
      context.fillRect(x, y, scale, scale);
      paths.push(`M${x} ${y}h${scale}v${scale}h-${scale}z`);
    }
  }
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="0 0 ${size} ${size}" shape-rendering="crispEdges"><rect width="${size}" height="${size}" fill="#fff"/><path fill="#000" d="${paths.join('')}"/></svg>`;
  const png = canvas.toDataURL('image/png');
  const jpeg = canvas.toDataURL('image/jpeg', 0.95);
  if (!png.startsWith('data:image/png;') || !jpeg.startsWith('data:image/jpeg;')) throw new Error(t('Этот браузер не поддерживает выбранный формат изображения.'));
  return {svg, png, jpeg};
}
