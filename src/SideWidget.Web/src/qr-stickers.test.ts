// @vitest-environment node
import {describe,it,expect} from 'vitest';
import QRCode from 'qrcode';
import sharp from 'sharp';
import jsQR from 'jsqr';
import {stickerLayout,stickerDocument,defaultStickerOptions} from './qr-stickers';

describe('QR sticker printing',()=>{
 it('uses physical pages, one vector QR per sticker and escaped caption',()=>{
  const html=stickerDocument('  Привет\nмир  ',{...defaultStickerOptions,copies:3,caption:'</div><script>bad()</script>'});
  expect(html).toContain('size:60mm 60mm');
  expect(html.match(/class="sticker"/g)).toHaveLength(3);
  expect(html.match(/<svg /g)).toHaveLength(3);
  expect(html).toContain('&lt;script&gt;');expect(html).not.toContain('<script>');
  expect(html).toContain('break-after:page');
 });
 it.each([{width:NaN},{width:19},{height:15},{padding:0},{padding:11},{copies:0},{copies:201},{copies:1.5}])('rejects invalid options %j',invalid=>{
  expect(()=>stickerLayout('test',{...defaultStickerOptions,...invalid})).toThrow();
 });
 it('rejects dense content at a small physical size',()=>{
  expect(()=>stickerLayout('Ж'.repeat(600),{...defaultStickerOptions,width:20,height:16,padding:2})).toThrow(/увеличьте|помещается/i);
 });
 it('keeps four quiet modules and decodes exact Unicode and whitespace at printed size',async()=>{
  const text='  Кириллица\nстрока 2 😀  ';
  const layout=stickerLayout(text,defaultStickerOptions);
  const modules=QRCode.create(text,{errorCorrectionLevel:'M'}).modules.size;
  expect(layout.svg).toContain(`viewBox="0 0 ${modules+8} ${modules+8}"`);
  const {data,info}=await sharp(Buffer.from(layout.svg)).resize(660,660,{kernel:'nearest'}).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  expect(jsQR(new Uint8ClampedArray(data),info.width,info.height)?.data).toBe(text);
 });
 it('uses the available square and reserves caption outside the QR',()=>{
  const layout=stickerLayout('hello',{...defaultStickerOptions,width:58,height:40,caption:'Test'});
  expect(layout.qrMm).toBe(30);expect(layout.innerWidth).toBe(54);
 });
 it('keeps 200 dense labels within the native bridge size limit',()=>{
  const html=stickerDocument('Ж'.repeat(600),{...defaultStickerOptions,width:200,height:200,copies:200});
  expect(html.match(/class="sticker"/g)).toHaveLength(200);
  expect(html.match(/<path /g)).toHaveLength(1);
  expect(html.length).toBeLessThan(1500000);
 });
});
