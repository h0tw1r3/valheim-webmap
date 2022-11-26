import ui from './ui';
import constants from "./constants";
import onPointers from "./onPointers";

const { canvas, map, mapBorder, mapBorderCircle } = ui;

let width = constants.CANVAS_WIDTH;
let height = constants.CANVAS_HEIGHT;
let exploreRadius = constants.EXPLORE_RADIUS;
let pixelSize = constants.PIXEL_SIZE;
let coordOffset = constants.COORD_OFFSET;

// preload map icons.
const mapIconImage = document.createElement('img');
mapIconImage.src = 'mapIcons.png';

const ctx = canvas.getContext('2d');

let mapImage;
let fogImage;
const fogCanvas = document.createElement('canvas');
const fogCanvasCtx = fogCanvas.getContext('2d');

let currentZoom = 100;

const mapIcons = [];
const hiddenIcons = {};
let followIcon;

const createIconEl = (iconObj) => {
    const iconEl = document.createElement('div');
    iconEl.id = iconObj.id;
    iconEl.className = `mapText mapIcon ${iconObj.type}`;
    if (iconObj.zIndex) {
        iconEl.style.zIndex = iconObj.zIndex;
    }
    const iconTextEl = document.createElement('div');
    iconTextEl.className = 'center text';
    iconTextEl.textContent = iconObj.text;
    iconEl.appendChild(iconTextEl);
    if (iconObj.node) {
        iconEl.appendChild(iconObj.node);
    }
    return iconEl;
};

const centerOnIcon = (iconObj) => {
    const rect = iconObj.el.getBoundingClientRect();
    const deltaX = window.innerWidth / 2 - rect.left;
    const deltaY = window.innerHeight / 2 - rect.top;
    if (Math.abs(deltaX) > 0.5 || Math.abs(deltaY) > 0.5) {
        map.style.left = deltaX + map.offsetLeft + 'px';
        map.style.top = deltaY + map.offsetTop + 'px';
    }
};

const setFollowIcon = (iconObj) => {
    followIcon = iconObj;
    if (followIcon) {
        centerOnIcon(followIcon);
    }
};

let pendingIconUpdate = false;
const updateIcons = () => {
    clearTimeout(performUpdateIcons);
    pendingIconUpdate = true;
    setTimeout(performUpdateIcons, 250);
};

const performUpdateIcons = () => {
    pendingIconUpdate = false;
    mapIcons.forEach(iconObj => {
        let firstRender = false;
        if (!iconObj.el) {
            firstRender = true;
            iconObj.el = createIconEl(iconObj);
            map.appendChild(iconObj.el);
        }

        const isIconTypeHidden = (iconObj.type in hiddenIcons && hiddenIcons[iconObj.type]);
        iconObj.el.style.display = (isIconTypeHidden || iconObj.hidden) ? 'none' : 'block';

        if (iconObj.flags) {
            Object.keys(iconObj.flags).forEach(key => {
                if (iconObj.flags[key]) {
                    iconObj.el.classList.add(key);
                } else {
                    iconObj.el.classList.remove(key);
                }
            });
        }

        if (!firstRender && iconObj.static) {
            return;
        }

        const imgX = iconObj.x / pixelSize + coordOffset;
        const imgY = height - (iconObj.z / pixelSize + coordOffset);

        iconObj.el.style.left = 100 * imgX / width + '%';
        iconObj.el.style.top = 100 * imgY / height + '%';
    });

    if (followIcon) {
        centerOnIcon(followIcon);
    }
};

window.addEventListener('mousemove', e => {
    const canvasOffsetScale = map.offsetWidth / width;
    const x = pixelSize * (-coordOffset + (e.clientX - map.offsetLeft) / canvasOffsetScale);
    const y = pixelSize * (height - coordOffset + (map.offsetTop - e.clientY) / canvasOffsetScale);
    ui.coords.textContent = `${x.toFixed(2)} , ${y.toFixed(2)}`;
});

const addIcon = (iconObj, update = true) => {
    if (!iconObj.id) {
        iconObj.id = `id_${Date.now()}_${Math.random()}`;
    }

    mapIcons.push(iconObj);
    if (update) {
        updateIcons();
    }
};

const hideIcon = (iconObj) => {
    const idx = mapIcons.indexOf(iconObj);
    iconObj.hidden = true;
    if (idx > -1 && iconObj.el) {
        iconObj.el.style.display = 'none';
    }
};

const hideIconById = (iconId) => {
    const iconObj = mapIcons.find(icon => icon.id === iconId);
    if (iconObj) {
        hideIcon(iconObj);
    }
};

const showIcon = (iconObj) => {
    const idx = mapIcons.indexOf(iconObj);
    iconObj.hidden = false;
    if (idx > -1 && iconObj.el) {
        iconObj.el.style.display = 'block';
    }
};

const showIconById = (iconId) => {
    const iconObj = mapIcons.find(icon => icon.id === iconId);
    if (iconObj) {
        showIcon(iconObj);
    }
};

const removeIcon = (iconObj) => {
    const idx = mapIcons.indexOf(iconObj);
    if (idx > -1) {
        mapIcons.splice(idx, 1);
        if (iconObj.el) {
            iconObj.el.remove();
            iconObj.el = undefined;
        }
    }
};

const removeIconById = (iconId) => {
    const iconToRemove = mapIcons.find(icon => icon.id === iconId);
    if (iconToRemove) {
        removeIcon(iconToRemove);
    }
};

const setIconTypeHidden = (type, isHidden) => {
    hiddenIcons[type] = isHidden;
};

const redrawMap = () => {
    ctx.clearRect(0, 0, width, height);
    ctx.globalCompositeOperation = 'source-over';
    ctx.drawImage(mapImage, 0, 0);
    ctx.globalCompositeOperation = 'multiply';
    ctx.drawImage(fogCanvas, 0, 0);

    updateIcons();
};

const explore = (mapX, mapZ) => {
    const radius = exploreRadius / pixelSize;
    const x = mapX / pixelSize + coordOffset;
    const y = height - (mapZ / pixelSize + coordOffset);
    fogCanvasCtx.beginPath();
    fogCanvasCtx.arc(x, y, radius, 0, 2 * Math.PI);
    fogCanvasCtx.fill();
    redrawMap();
};

const setZoom = function (zoomP, zoomTowardsX, zoomTowardsY) {
    if (zoomTowardsX === undefined) {
        zoomTowardsX = window.innerWidth / 2;
        zoomTowardsY = window.innerHeight / 2;
    }
    const oldZoom = currentZoom;
    const minZoom = 50;
    const maxZoom = 8000 * devicePixelRatio;
    zoomP = Math.min(Math.max(Math.round(zoomP), minZoom), maxZoom);
    currentZoom = zoomP;
    map.style.width = `${zoomP}%`;
    map.style.height = map.offsetWidth + 'px';

    const zoomRatio = currentZoom / oldZoom;
    map.style.left = zoomRatio * (map.offsetLeft - zoomTowardsX) + zoomTowardsX + 'px';
    map.style.top = zoomRatio * (map.offsetTop - zoomTowardsY) + zoomTowardsY + 'px';

    updateIcons();
};

let zoomingClassTimeout;
const removeZoomingClass = () => {
    clearTimeout(zoomingClassTimeout);
    zoomingClassTimeout = setTimeout(() => {
        map.classList.remove('zooming');
    }, 100);
};

const init = (options) => {
    width = constants.CANVAS_WIDTH;
    height = constants.CANVAS_HEIGHT;
    exploreRadius = constants.EXPLORE_RADIUS;
    pixelSize = constants.PIXEL_SIZE;
    coordOffset = constants.COORD_OFFSET;

    canvas.width = width;
    canvas.height = height;
    map.style.width = '100%';
    map.style.height = map.offsetWidth + 'px';
    map.style.left = (window.innerWidth - map.offsetWidth) / 2 + 'px';
    map.style.top = (window.innerHeight - map.offsetHeight) / 2 + 'px';
    fogCanvas.width = width;
    fogCanvas.height = height;
    fogCanvasCtx.fillStyle = '#ffffff';

    mapBorder.setAttribute("viewBox", `0 0 ${width} ${height}`);
    mapBorderCircle.setAttribute("cx", width / 2);
    mapBorderCircle.setAttribute("cy", width / 2);
    mapBorderCircle.setAttribute("r", width * 0.4275);

    mapImage = options.mapImage;
    fogImage = options.fogImage;

    fogCanvasCtx.drawImage(fogImage, 0, 0);

    redrawMap();

    const zoomChange = (e, mult = 1) => {
        map.classList.add('zooming');
        const oldZoom = currentZoom;
        const zoomAmount = Math.max(Math.floor(oldZoom / 5), 1) * mult;
        const scrollAmt = e.deltaY === 0 ? e.deltaX : e.deltaY;
        if (scrollAmt > 0) {
            // zoom out.
            setZoom(oldZoom - zoomAmount, e.clientX, e.clientY);
        } else {
            // zoom in.
            setZoom(oldZoom + zoomAmount, e.clientX, e.clientY);
        }
        removeZoomingClass();
    };

    if (options.zoom) {
        setZoom(options.zoom);
    }

    window.addEventListener('wheel', zoomChange);
    window.addEventListener('resize', () => {
        map.style.height = map.offsetWidth + 'px';
    });

    const canvasPreDragPos = {};
    let isZooming = false;
    let lastZoomDist;
    onPointers(window, {
        down: (pointers) => {
            if (pointers.length === 1) {
                canvasPreDragPos.x = map.offsetLeft;
                canvasPreDragPos.y = map.offsetTop;
            } else if (pointers.length === 2) {
                isZooming = true;
                lastZoomDist = undefined;
            }
        },
        move: (pointers) => {
            if (pointers.length === 1 && !isZooming && !followIcon) {
                const e = pointers[0].event;
                map.style.left = canvasPreDragPos.x + (e.clientX - pointers[0].downEvent.clientX) + 'px';
                map.style.top = canvasPreDragPos.y + (e.clientY - pointers[0].downEvent.clientY) + 'px';

                updateIcons();
            } else if (pointers.length === 2) {
                const x1 = pointers[0].event.clientX;
                const y1 = pointers[0].event.clientY;
                const x2 = pointers[1].event.clientX;
                const y2 = pointers[1].event.clientY;
                const diffX = x1 - x2;
                const diffY = y1 - y2;
                const dist = Math.sqrt(diffX * diffX + diffY * diffY);
                if (lastZoomDist) {
                    const diffDist = (lastZoomDist - dist) || -1;
                    zoomChange({
                        deltaY: diffDist,
                        clientX: (x1 + x2) / 2,
                        clientY: (y1 + y2) / 2
                    }, 0.08);
                }
                lastZoomDist = dist;
            }
        },
        up: (pointers) => {
            if (pointers.length === 0) {
                isZooming = false;
            }
        }
    });
};

export default {
    init,
    addIcon,
    removeIcon,
    removeIconById,
    hideIcon,
    hideIconById,
    showIcon,
    showIconById,
    setIconTypeHidden,
    explore,
    centerOnIcon,
    setFollowIcon,
    update: redrawMap,
    updateIcons,
    canvas
};

