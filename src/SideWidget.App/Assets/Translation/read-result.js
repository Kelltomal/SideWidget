((request) => {
    // Fixed selectors observed on the text-translation pages. Never return page
    // HTML, dictionaries, account data or arbitrary scripts to the local bridge.
    const visible = element => {
        for (let node = element; node; node = node.parentElement) {
            const style = getComputedStyle(node);
            if (node.hidden || style.display === 'none' || style.visibility === 'hidden' || style.opacity === '0') return false;
        }
        return true;
    };
    const manualForms = [...document.querySelectorAll('form[action*="showcaptcha"],form[action*="/sorry"]')]
        .some(form => [...form.querySelectorAll('input:not([type="hidden"]),button')].some(visible));
    const challengeFrames = [...document.querySelectorAll('iframe[src*="recaptcha"],iframe[src*="smartcaptcha"]')]
        .some(frame => visible(frame) && getComputedStyle(frame).width !== '0px' && getComputedStyle(frame).height !== '0px');
    if (manualForms || challengeFrames)
        return { state: 'blocked' };
    const normalize = value => value.replace(/\r\n/g, '\n');
    const languageMatches = value => value?.toLowerCase().split('-')[0] === request.target;
    const input = request.provider === 'google'
        ? document.querySelector('textarea[aria-label]') || document.querySelector('textarea')
        : document.querySelector('textarea#textarea');
    if (!input || normalize(input.value) !== normalize(request.text)) return { state: 'waiting' };
    let text = '';
    if (request.provider === 'google') {
        const paragraphs = [...document.querySelectorAll('.HwtZe[lang]')]
            .filter(element => languageMatches(element.getAttribute('lang')))
            .map(element => [...element.querySelectorAll('.ryNqvb')].map(span => span.textContent).join(' '));
        text = paragraphs.join('\n');
    } else {
        if (!languageMatches(document.querySelector('#textbox2[lang]')?.getAttribute('lang'))) return { state: 'waiting' };
        const editor = document.querySelector('#dstTextField [data-lexical-editor="true"]');
        if (editor) text = [...editor.children].map(element => element.textContent).join('\n') || editor.textContent;
        else text = document.querySelector('#translation')?.textContent || '';
    }
    return text.trim() ? { state: 'ready', text: text.slice(0, 40001) } : { state: 'waiting' };
})(__REQUEST_JSON__)
