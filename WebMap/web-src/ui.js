const ui = {};
const allUi = document.querySelectorAll('[data-id]');
allUi.forEach(el => {
    ui[el.dataset.id] = el;
});

const tempDiv = document.createElement('div');
export const createUi = (html) => {
    tempDiv.innerHTML = html;

    const uiEls = {};
    const dataEls = tempDiv.querySelectorAll('[data-id]');
    dataEls.forEach(el => {
        uiEls[el.dataset.id] = el;
    });

    return {
        el: tempDiv.children[0],
        ui: uiEls
    };
};

export default ui;
