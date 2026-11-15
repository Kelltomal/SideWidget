// @vitest-environment jsdom
import {readFileSync} from 'node:fs';
import {expect,it,afterEach} from 'vitest';
const script=readFileSync('../SideWidget.App/Assets/Translation/read-result.js','utf8');
const request={provider:'google',text:'Hello',target:'ru'};
function read(extra:object={}){return window.eval(script.replace('__REQUEST_JSON__',JSON.stringify({...request,...extra})));}
afterEach(()=>{document.body.innerHTML='';});
it('reads only Google translation spans in target language',()=>{
 document.body.innerHTML='<textarea>Hello</textarea><span class="HwtZe" lang="ru"><span class="ryNqvb">Привет</span><div>dictionary noise</div><span class="ryNqvb">мир</span></span>';
 expect(read()).toEqual({state:'ready',text:'Привет мир'});
});
it('rejects stale source and wrong target',()=>{
 document.body.innerHTML='<textarea>old</textarea><span class="HwtZe" lang="de"><span class="ryNqvb">Hallo</span></span>';
 expect(read()).toEqual({state:'waiting'});
 (document.querySelector('textarea')!).value='Hello';expect(read()).toEqual({state:'waiting'});
});
it('reads Yandex modern paragraphs without buttons or legacy duplicates',()=>{
 document.body.innerHTML='<textarea id="textarea">Hello</textarea><div id="textbox2" lang="ru"></div><div id="dstTextField"><div data-lexical-editor="true"><p>Привет</p><p>мир</p></div><button>Копировать</button></div><pre id="translation">old</pre>';
 expect(read({provider:'yandex'})).toEqual({state:'ready',text:'Привет\nмир'});
});
it('reads Yandex legacy result when modern editor is absent',()=>{
 document.body.innerHTML='<textarea id="textarea">Hello</textarea><div id="textbox2" lang="ru"></div><pre id="translation"><span>Привет</span> мир</pre>';
 expect(read({provider:'yandex'})).toEqual({state:'ready',text:'Привет мир'});
});
it('detects verification without returning its page text',()=>{
 document.body.innerHTML='<form action="/showcaptcha"><input></form>';
 expect(read()).toEqual({state:'blocked'});
});
it('does not show an automatic hidden verification form',()=>{
 document.body.innerHTML='<form action="/showcaptcha"><input type="hidden" name="fingerprint"></form>';
 expect(read()).toEqual({state:'waiting'});
});
it('ignores hidden CAPTCHA frames after verification but shows a visible one',()=>{
 document.body.innerHTML='<div style="display:none"><iframe src="https://smartcaptcha.yandexcloud.net/"></iframe></div><textarea>Hello</textarea><span class="HwtZe" lang="ru"><span class="ryNqvb">Привет</span></span>';
 expect(read()).toEqual({state:'ready',text:'Привет'});
 (document.querySelector('div')!).style.display='block';
 expect(read()).toEqual({state:'blocked'});
});
it('treats markup-like text as text, never code',()=>{
 document.body.innerHTML='<textarea></textarea><span class="HwtZe" lang="ru"><span class="ryNqvb">&lt;b&gt;Текст&lt;/b&gt;</span></span>';
 const text='"; window.injected=true; //';document.querySelector('textarea')!.value=text;
 expect(read({text})).toEqual({state:'ready',text:'<b>Текст</b>'});
 expect((window as unknown as {injected?:boolean}).injected).toBeUndefined();
});


it('accepts Google regional Chinese and rejects a Yandex target mismatch',()=>{
 document.body.innerHTML='<textarea>Hello</textarea><span class="HwtZe" lang="zh-CN"><span class="ryNqvb">你好</span></span>';
 expect(read({target:'zh'})).toEqual({state:'ready',text:'你好'});
 document.body.innerHTML='<textarea id="textarea">Hello</textarea><div id="textbox2" lang="de"></div><pre id="translation">Hallo</pre>';
 expect(read({provider:'yandex'})).toEqual({state:'waiting'});
});
