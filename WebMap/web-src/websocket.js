const actionListeners = {};

const addActionListener = (type, func) => {
	const listeners = actionListeners[type] || [];
	listeners.push(func);
	actionListeners[type] = listeners;
};

const actions = {
	players: (lines, message) => {
		const msg = message.replace(/^players\n/, '');
		const playerSections = msg.split('\n\n');
		const playerData = [];
		playerSections.forEach(playerSection => {
			const playerLines = playerSection.split('\n');
			const newPlayer = {
				id: playerLines[0],
				name: playerLines[1],
				health: parseFloat(playerLines[3]),
				maxHealth: parseFloat(playerLines[4]),
				stamina: parseFloat(playerLines[5]),
				maxStamina: parseFloat(playerLines[6])
			};

			if (playerLines[2] !== 'hidden') {
				const xyz = playerLines[2].split(',').map(parseFloat);
				newPlayer.x = xyz[0];
				newPlayer.y = xyz[1];
				newPlayer.z = xyz[2];
			} else {
				newPlayer.hidden = true;
			}
			playerData.push(newPlayer);
		});
		// const fakePlayer = playerData[0];
		// if (fakePlayer) {
		// 	playerData.push({ ...fakePlayer, x: fakePlayer.x + 1000, z: fakePlayer.z - 2000, id: 'asd', name: 'lolol' });
		// }

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
	});

	ws.addEventListener('close', () => {
		connectionTries++;
		const seconds = Math.min(connectionTries * (connectionTries + 1), 120);
		setTimeout(init, seconds * 1000);
	});
};

export default {
	init,
	addActionListener
};

