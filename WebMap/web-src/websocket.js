const actionListeners = {};

const addActionListener = (type, func) => {
    const listeners = actionListeners[type] || [];
    listeners.push(func);
    actionListeners[type] = listeners;
};

const getActionListeners = (type) => {
    return actionListeners[type] || [];
}

const actions = {
    players: (lines, message) => {
        const msg = message.replace(/^players\n/, '');
        const playerSections = msg.split('\n\n');
        const playerData = [];
        playerSections.forEach(playerSection => {
            const playerLines = playerSection.split('\n');
            if (typeof playerLines[2] === 'undefined') {
                return;
            }
            const newPlayer = {
                id: playerLines.shift(),
                name: playerLines.shift(),
                health: playerLines.shift(),
                maxHealth: playerLines.shift()
            };

            if (playerLines[0] == 'hidden') {
                newPlayer.hidden = true;
                playerLines.shift();
            } else {
                newPlayer.hidden = false;
            }
            if (typeof playerLines[0] !== 'undefined') {
                const xyz = playerLines[0].split(',').map(parseFloat);
                newPlayer.x = xyz[0];
                newPlayer.z = xyz[1];
            }
            playerData.push(newPlayer);
        });

        actionListeners.players.forEach(func => {
            func(playerData);
        });
    },
    ping: (lines) => {
        const xz = lines[2].split(',');
        const ping = {
            playerId: lines[0],
            name: lines[1],
            x: parseFloat(xz[0]),
            z: parseFloat(xz[1])
        };
        actionListeners.ping.forEach(func => {
            func(ping);
        });
    },
    pin: (lines) => {
        const xz = lines[4].split(',').map(parseFloat);
        const pin = {
            id: lines[1],
            uid: lines[0],
            type: lines[2],
            name: lines[3],
            x: xz[0],
            z: xz[1],
            text: lines[5]
        };
        actionListeners.pin.forEach(func => {
            func(pin);
        });
    },
    rmpin: (lines) => {
        actionListeners.rmpin.forEach(func => {
            func(lines[0]);
        });
    },
    messages: (lines, message) => {
        const msg = message.replace(/^messages\n/, '');
        var messages = JSON.parse(msg);
        actionListeners.messages.forEach(func => {
            func(messages);
        });
    },
    reload: (lines) => {
	window.history.forward(1);
    }
};

Object.keys(actions).forEach(key => {
    actionListeners[key] = [];
});

let connectionTries = 0;
const init = () => {
    const websocketUrl = location.href.split('?')[0].replace(/^http/, 'ws');
    const ws = new WebSocket(websocketUrl);
    ws.addEventListener('message', (e) => {
        const message = e.data.trim();
        const lines = message.split('\n');
        const action = lines.shift();
        const actionFunc = actions[action];
        if (actionFunc) {
            actionFunc(lines, message);
        } else {
            console.log("unknown websocket message: ", e.data);
        }
    });

    ws.addEventListener('open', () => {
        connectionTries = 0;
        ws.send('players');
    });

    ws.addEventListener('close', () => {
        connectionTries++;
        const seconds = Math.min(connectionTries * (connectionTries + 1), 120);
        setTimeout(init, seconds * 1000);
    });
};

export default {
    init,
    addActionListener,
    getActionListeners
};

