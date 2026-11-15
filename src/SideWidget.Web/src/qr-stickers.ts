import QRCode from 'qrcode';

export type StickerOptions={width:number;height:number;padding:number;copies:number;caption:string;level:'L'|'M'|'Q'|'H'};
export const defaultStickerOptions:StickerOptions={width:60,height:60,padding:2,copies:1,caption:'',level:'M'};
export const stickerFormats=[
 {id:'60x60',width:60,height:60,padding:2}, {id:'58x40',width:58,height:40,padding:2},
 {id:'58x60',width:58,height:60,padding:2}, {id:'43x25',width:43,height:25,padding:2},
 {id:'30x20',width:30,height:20,padding:2}, {id:'120x75',width:120,height:75,padding:3},
];
const escapeHtml=(value:string)=>value.replace(/[&<>"']/g,char=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[char]!));

export function stickerLayout(text:string,options:StickerOptions){
 const {width,height,padding,copies,caption,level}=options;
 if(!Number.isFinite(width)||width<20||width>200)throw new Error('Ширина стикера — от 20 до 200 мм.');
 if(!Number.isFinite(height)||height<16||height>200)throw new Error('Высота стикера — от 16 до 200 мм.');
 if(!Number.isFinite(padding)||padding<1||padding>10)throw new Error('Отступ — от 1 до 10 мм.');
 if(!Number.isInteger(copies)||copies<1||copies>200)throw new Error('Количество стикеров — целое число от 1 до 200.');
 if(!text||text.length>6000||new TextEncoder().encode(text).length>2953)throw new Error('Введите текст, который помещается в один QR.');
 if(!['L','M','Q','H'].includes(level))throw new Error('Неизвестная коррекция QR.');
 if(caption.length>160)throw new Error('Подпись — не больше 160 символов.');
 if(/[\uD800-\uDBFF](?![\uDC00-\uDFFF])|(?<![\uD800-\uDBFF])[\uDC00-\uDFFF]/u.test(text))throw new Error('Текст содержит неполный символ Unicode.');
 const innerWidth=width-2*padding;
 const qrMm=Math.min(innerWidth,height-2*padding-(caption?6:0));
 let modules:ReturnType<typeof QRCode.create>['modules'];
 try{modules=QRCode.create(text,{errorCorrectionLevel:level}).modules;}catch{throw new Error('Текст не помещается в QR. Сократите текст или измените коррекцию.');}
 const total=modules.size+8;
 if(qrMm/total<0.25)throw new Error('QR слишком плотный для стикера: увеличьте размер или сократите текст.');
 const paths:string[]=[];
 for(let row=0;row<modules.size;row++)for(let col=0;col<modules.size;col++)if(modules.get(row,col))paths.push(`M${col+4} ${row+4}h1v1h-1z`);
 const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="${qrMm}mm" height="${qrMm}mm" viewBox="0 0 ${total} ${total}" shape-rendering="crispEdges"><rect width="${total}" height="${total}" fill="#fff"/><path fill="#000" d="${paths.join('')}"/></svg>`;
 return {svg,qrMm,innerWidth};
}

export function stickerDocument(text:string,options:StickerOptions){
 const {svg,qrMm}=stickerLayout(text,options);
 const {width,height,padding,copies,caption}=options;
 // Reuse vector geometry across pages, keeping 200 dense labels below bridge limits.
 const opening=svg.slice(0,svg.indexOf('>')+1);
 const drawing=svg.slice(svg.indexOf('>')+1,-6);
 const pages=Array.from({length:copies},(_,index)=>`<section class="sticker"><div class="content">${opening}${index===0?`<defs><g id="qr-code">${drawing}</g></defs>`:''}<use href="#qr-code"/></svg>${caption?`<div class="caption">${escapeHtml(caption)}</div>`:''}</div></section>`).join('');
 return `<!doctype html><html lang="ru"><head><meta charset="utf-8"><title>SideWidget — QR-стикеры</title><style>
 @page{size:${width}mm ${height}mm;margin:0}
 *{box-sizing:border-box}html,body{margin:0;padding:0;background:#fff;color:#000}
 .sticker{width:${width}mm;height:${height}mm;padding:${padding}mm;display:flex;align-items:center;justify-content:center;break-inside:avoid;break-after:page;overflow:hidden}
 .sticker:last-child{break-after:auto}.content{width:100%;display:flex;flex-direction:column;align-items:center;gap:0}
 svg{display:block;width:${qrMm}mm;height:${qrMm}mm;flex:none}
 .caption{width:100%;height:6mm;padding-top:1mm;font:2.3mm/4mm 'Segoe UI',sans-serif;text-align:center;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
 @media print{html,body{width:${width}mm;print-color-adjust:exact;-webkit-print-color-adjust:exact}}
 </style></head><body>${pages}</body></html>`;
}
