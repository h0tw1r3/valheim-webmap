import constants from "./constants";
import websocket from "./websocket";
import map from "./map";
import players from "./players";
import ui, { createUi } from "./ui";

const mapImage = document.createElement('img');
const fogImage = document.createElement('img');

const fetchMap = () => new Promise((res) => {
    fetch('map').then(res => res.blob()).then((mapBlob) => {
        mapImage.onload = res;
        mapImage.src = URL.createObjectURL(mapBlob);
    });
});

const fetchFog = () => new Promise((res) => {
    fogImage.onload = res;
    fogImage.src = 'fog';
});

const createStyleSheet = (styles = '') => {
    const style = document.createElement("style");
    style.appendChild(document.createTextNode(styles));
    document.head.appendChild(style);
    return style.sheet;
};

const parseVector3 = str => {
    const strParts = str.split(',');
    return {
        x: parseFloat(strParts[0]),
        y: parseFloat(strParts[1]),
        z: parseFloat(strParts[2]),
    };
};

const fetchConfig = fetch('config').then(res => res.json()).then(config => {
    constants.CANVAS_WIDTH = config.texture_size || 2048;
    constants.CANVAS_HEIGHT = config.texture_size || 2048;
    constants.PIXEL_SIZE = config.pixel_size || 12;
    constants.EXPLORE_RADIUS = config.explore_radius || 100;
    constants.UPDATE_INTERVAL = config.update_interval || 0.5;
    constants.WORLD_NAME = config.world_name;
    constants.WORLD_START_POSITION = parseVector3(config.world_start_pos);
    constants.DEFAULT_ZOOM = config.default_zoom || 200;
    constants.MAX_MESSAGES = config.max_messages || 100;
    document.title = `Valheim WebMap - ${constants.WORLD_NAME}`;
    createStyleSheet(`
		.mapIcon.player {
			transition: top ${constants.UPDATE_INTERVAL}s linear, left ${constants.UPDATE_INTERVAL}s linear;
		}
		.map.smooth {
			transition: top ${constants.UPDATE_INTERVAL}s linear, left ${constants.UPDATE_INTERVAL}s linear;
		}
	`);
});

const setup = async () => {
    websocket.init();
    players.init();

    await Promise.all([
        fetchMap(),
        fetchFog(),
        fetchConfig
    ]);

    map.init({
        mapImage,
        fogImage,
        zoom: constants.DEFAULT_ZOOM
    });

    map.addIcon({
        type: 'start',
        x: constants.WORLD_START_POSITION.x,
        z: constants.WORLD_START_POSITION.z
    });

    const pings = {};
    websocket.addActionListener('ping', (ping) => {
        let mapIcon = pings[ping.playerId];
        if (!mapIcon) {
            mapIcon = { ...ping };
            mapIcon.type = 'ping';
            mapIcon.text = ping.name;
            map.addIcon(mapIcon, false);
            pings[ping.playerId] = mapIcon;
        }
        mapIcon.x = ping.x;
        mapIcon.z = ping.z;
        map.updateIcons();

        clearTimeout(mapIcon.timeoutId);
        mapIcon.timeoutId = setTimeout(() => {
            delete pings[ping.playerId];
            map.removeIcon(mapIcon);
        }, 8000);
    });

    fetch('pins').then(res => res.text()).then(text => {
        const lines = text.split('\n');
        lines.forEach(line => {
            const lineParts = line.split(',');
            if (lineParts.length > 5) {
                const pin = {
                    id: lineParts[1],
                    uid: lineParts[0],
                    type: lineParts[2],
                    name: lineParts[3],
                    x: lineParts[4],
                    z: lineParts[5],
                    text: lineParts[6],
                    static: true
                };
                map.addIcon(pin, false);
            }
        });
        map.updateIcons();
    });

    websocket.addActionListener('pin', (pin) => {
        map.addIcon(pin);
    });

    websocket.addActionListener('rmpin', (pinid) => {
        map.removeIconById(pinid);
    });

    websocket.addActionListener('messages', (messages) => {
        messages.forEach((message) => {
            const messageEntry = createUi(`
                <div class="message">
		            <span class="name" data-id="name"></span>: <span class="text" data-id="message"></span>
                </div>
            `);

            messageEntry.ui.name.textContent = message.name;
            messageEntry.ui.message.textContent = message.message;
            ui.messageList.appendChild(messageEntry.el);
        });
        while (document.getElementById('messages').childElementCount > constants.MAX_MESSAGES) {
            document.getElementById('messages').childNodes[0].remove();
        }
    });

    fetch('messages').then(resp => resp.json()).then(messages => {
        if (messages.length > 0) {
            websocket.getActionListeners('messages').forEach(func => {
                func(messages);
            });
        }
    });

    window.addEventListener('resize', () => {
        map.update();
    });

    ui.menuBtn.addEventListener('click', () => {
        ui.menu.classList.toggle('menuOpen');
    });

    const closeMenu = (e) => {
        const inMenu = e.target.closest('.menu');
        const inMenuBtn = e.target.closest('.menu-btn');
        if (!inMenu && !inMenuBtn) {
            ui.menu.classList.remove('menuOpen');
        }
    };
    window.addEventListener('mousedown', closeMenu);
    window.addEventListener('touchstart', closeMenu);

    const hideCheckboxes = ui.menu.querySelectorAll('.hideIconCheckbox');
    hideCheckboxes.forEach(el => {
        el.addEventListener('change', () => {
            map.setIconHidden(el.dataset.hide, el.checked || ui.hideAll.checked);
            if (el.dataset.hide === 'all') {
                hideCheckboxes.forEach(el2 => {
                    map.setIconHidden(el2.dataset.hide, el.checked || el2.checked);
                });
            }
            map.updateIcons();
        });
    });

    ui.hidePlayerList.addEventListener('change', () => {
        if (ui.hidePlayerList.checked) {
            ui.playerListContainer.style.right = -ui.playerListContainer.offsetWidth + 'px';
        } else {
            ui.playerListContainer.style.right = 0;
        }
    });
};

setup();
