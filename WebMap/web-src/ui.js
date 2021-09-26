const ui = {};
const allUi = document.querySelectorAll('[data-id]');
allUi.forEach(el => {
    ui[el.dataset.id] = el;
});

const tempDiv = document.createElement('div');
export const createUi = (html, tempEl = tempDiv) => {
    tempEl.innerHTML = html;

    const uiEls = {};
    const dataEls = tempEl.querySelectorAll('[data-id]');
    dataEls.forEach(el => {
        uiEls[el.dataset.id] = el;
    });

    return {
        el: tempEl.children[0],
        ui: uiEls
    };
};

export default ui;
