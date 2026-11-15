// @vitest-environment node
import { afterEach, describe, expect, it, vi } from 'vitest';
import { createCanvas } from '@napi-rs/canvas';
import sharp from 'sharp';
import jsQR from 'jsqr';
import { evaluate, unitCategories, convertUnit, parseColor, makeQr } from './tools';

describe('safe calculator', () => {
  it.each([
    ['2+3*4', '14'], ['(2+3)*4', '20'], ['-3*-2', '6'], ['-(2+3)', '-5'],
    ['6/2*3', '9'], ['2--3', '5'], ['1,5+2.25', '3.75'], ['.5 + ,5', '1'],
    ['200*10%', '20'], ['200+10%', '200.1'], ['(25+25)%', '0.5'],
    ['0.1+0.2', '0.3'], ['1/3', '0.333333333333333'], ['-0', '0'],
    ['1e-7*10', '0.000001'], ['1e20*10', '1e+21'],
    ['200 × 10%', '20'], ['(1250 + 350) × 0,2', '320'], ['12 ÷ 3 − 6', '-2'], ['−3 × −2', '6'],
  ])('%s = %s (15 significant digits, percent always divides by 100)', (expression, result) => {
    expect(evaluate(expression)).toBe(result);
  });
  it.each(['', ' ', '1/0', '0/0', '2+', '(2', '2)', '2(3)', '1 2', '1,2.3', '1,234,567', '2**3', '2^3', 'Math.random()', 'globalThis.x=1', 'Infinity', 'NaN', '1e309', '1e308*10'])('rejects invalid or nonfinite %s', input => {
    expect(() => evaluate(input)).toThrow();
  });
  it('bounds recursion and tokens before expensive parsing', () => {
    expect(() => evaluate('('.repeat(40) + '1' + ')'.repeat(40))).toThrow();
    expect(() => evaluate('-'.repeat(600) + '1')).toThrow();
    expect(() => evaluate('1+'.repeat(300) + '1')).toThrow();
    expect(() => evaluate('1'.repeat(5000))).toThrow();
  });
  it('accepts its own scientific notation output when reusing history', () => {
    expect(evaluate(evaluate('1/10000000'))).toBe('1e-7');
  });
  it('rejects overflow caused by rounding the largest finite double', () => {
    expect(() => evaluate('1.7976931348623157e308')).toThrow();
  });
});

describe('unit conversion', () => {
  it.each([
    [1, 'mi', 'm', 1609.344], [1, 'in', 'cm', 2.54], [1, 'ha', 'm2', 10000],
    [1, 'l', 'cm3', 1000], [1, 'lb', 'kg', 0.45359237], [0, 'C', 'F', 32],
    [32, 'F', 'C', 0], [-40, 'C', 'F', -40], [273.15, 'K', 'C', 0],
    [36, 'kmh', 'ms', 10], [1, 'day', 's', 86400], [1, 'MB', 'B', 1000000],
    [1, 'MiB', 'B', 1048576], [8, 'bit', 'B', 1], [1, 'GiB', 'MiB', 1024],
  ])('%s %s to %s', (value, from, to, expected) => {
    expect(convertUnit(value, from, to)).toBeCloseTo(expected, 9);
  });
  it('rejects cross-category units, unknown ids, nonfinite values and impossible absolute temperatures', () => {
    expect(() => convertUnit(1, 'm', 'kg')).toThrow();
    expect(() => convertUnit(1, 'watts', 'm')).toThrow();
    expect(() => convertUnit(Infinity, 'm', 'cm')).toThrow();
    expect(() => convertUnit(-274, 'C', 'K')).toThrow();
    expect(() => convertUnit(-1, 'K', 'C')).toThrow();
    expect(() => convertUnit(1e308, 'km', 'mm')).toThrow();
  });
  it('exposes eight categories and converts every exposed unit to itself', () => {
    expect(unitCategories).toHaveLength(8);
    const ids = unitCategories.flatMap(category => category.units.map(unit => unit.id));
    expect(new Set(ids).size).toBe(ids.length);
    for (const id of ids) expect(convertUnit(10, id, id)).toBeCloseTo(10, 9);
  });
});

describe('colors', () => {
  it.each([
    ['#f00', '#FF0000', 'rgb(255, 0, 0)', 'hsl(0, 100%, 50%)'],
    ['336699', '#336699', 'rgb(51, 102, 153)', 'hsl(210, 50%, 40%)'],
    ['rgb(0, 255, 0)', '#00FF00', 'rgb(0, 255, 0)', 'hsl(120, 100%, 50%)'],
    ['hsl(240, 100%, 50%)', '#0000FF', 'rgb(0, 0, 255)', 'hsl(240, 100%, 50%)'],
    ['hsl(360, 100%, 50%)', '#FF0000', 'rgb(255, 0, 0)', 'hsl(0, 100%, 50%)'],
    ['#fff', '#FFFFFF', 'rgb(255, 255, 255)', 'hsl(0, 0%, 100%)'],
    ['#000', '#000000', 'rgb(0, 0, 0)', 'hsl(0, 0%, 0%)'],
  ])('normalizes %s', (input, hex, rgb, hsl) => {
    expect(parseColor(input)).toEqual({hex, rgb, hsl});
  });
  it.each(['#abcd', '#12345678', 'red', '#xyz', 'rgb(256,0,0)', 'rgb(-1,0,0)', 'rgb(1.5,0,0)', 'rgba(0,0,0,1)', 'hsl(361,100%,50%)', 'hsl(0,101%,50%)', 'hsl(0,100%,-1%)', 'hsl(0,100,50)', '', 'rgb(0 1,2)'])('rejects %s without silent clipping or alpha loss', input => {
    expect(() => parseColor(input)).toThrow();
  });
});

describe('local QR export', () => {
  afterEach(() => vi.unstubAllGlobals());
  // Use real Canvas rasterization; jsdom has no native canvas implementation.
  function useCanvas() {
    vi.stubGlobal('document', { createElement: (tag: string) => {
      if (tag !== 'canvas') throw new Error(`Unexpected element ${tag}`);
      return createCanvas(1, 1);
    } });
  }
  async function decode(input: Buffer) {
    const {data, info} = await sharp(input).ensureAlpha().raw().toBuffer({resolveWithObject: true});
    return jsQR(new Uint8ClampedArray(data), info.width, info.height)?.data;
  }
  it.each(['Привет, мир!\nВторая строка 😀', 'https://example.com/?q=hello', 'Длинный текст. '.repeat(50)])('decodes vector SVG, PNG and JPEG back to original text (%s)', async text => {
    useCanvas();
    const result = await makeQr(text, {size: 768, level: 'M'});
    expect(result.svg).toContain('<svg');
    expect(result.svg).not.toMatch(/<image|data:image/);
    expect(await decode(Buffer.from(result.svg))).toBe(text);
    for (const [format, dataUrl] of [['png', result.png], ['jpeg', result.jpeg]]) {
      expect(dataUrl).toMatch(new RegExp(`^data:image/${format};base64,`));
      const bytes = Buffer.from(dataUrl.split(',')[1], 'base64');
      expect(await decode(bytes)).toBe(text);
      const metadata = await sharp(bytes).metadata();
      expect(metadata.width).toBe(768);
      expect(metadata.height).toBe(768);
      expect(metadata.format).toBe(format);
    }
  });
  it('keeps white margins and only black/white pixels in PNG', async () => {
    useCanvas();
    const {png} = await makeQr('A', {size: 290, level: 'L'});
    const {data, info} = await sharp(Buffer.from(png.split(',')[1], 'base64')).ensureAlpha().raw().toBuffer({resolveWithObject: true});
    expect(info.channels).toBe(4);
    for (let y = 0; y < info.height; y++) for (let x = 0; x < info.width; x++) {
      const index = (y * info.width + x) * 4;
      const red = data[index];
      const margin = x < 40 || y < 40 || x >= 250 || y >= 250;
      if (data[index + 3] !== 255 || data[index + 1] !== red || data[index + 2] !== red ||
          (red !== 255 && (red !== 0 || margin))) {
        throw new Error(`Unexpected QR pixel at (${x}, ${y}): ${data.subarray(index, index + 4).join(', ')}`);
      }
    }
  });
  it('handles every correction level and preserves leading/trailing whitespace', async () => {
    useCanvas();
    for (const level of ['L', 'M', 'Q', 'H'] as const) {
      const {svg} = await makeQr('  Текст\n ', {level});
      expect(await decode(Buffer.from(svg))).toBe('  Текст\n ');
    }
  });
  it('rejects oversized text, malformed UTF-16 and invalid render options without truncating', async () => {
    useCanvas();
    await expect(makeQr('')).rejects.toThrow();
    await expect(makeQr('а'.repeat(1600))).rejects.toThrow();
    await expect(makeQr('a'.repeat(2000), {level: 'H'})).rejects.toThrow();
    await expect(makeQr('\uD800')).rejects.toThrow();
    await expect(makeQr('A', {size: 10})).rejects.toThrow();
    await expect(makeQr('A', {size: NaN})).rejects.toThrow();
    await expect(makeQr('A', {level: 'bad' as 'L'})).rejects.toThrow();
    await expect(makeQr('x'.repeat(1000), {size: 128})).rejects.toThrow();
  });
});
