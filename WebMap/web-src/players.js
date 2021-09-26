import ui, { createUi } from "./ui";
import websocket from "./websocket";
import map from "./map";

const playerMapIcons = {};
let followingPlayer;

const followPlayer = (playerMapIcon) => {
    if (followingPlayer) {
        followingPlayer.playerListEntry.el.classList.remove('selected');
    }
    if (playerMapIcon && playerMapIcon !== followingPlayer) {
        followingPlayer = playerMapIcon;
        followingPlayer.playerListEntry.el.classList.add('selected');
        ui.map.classList.remove('smooth');
        map.setFollowIcon(playerMapIcon);
        ui.topMessage.textContent = `Following ${followingPlayer.name}`;
        setTimeout(() => {
            ui.map.classList.add('smooth');
        }, 0);
    } else {
        followingPlayer = null;
        map.setFollowIcon(null);
        ui.map.classList.remove('smooth');
        ui.topMessage.textContent = '';
    }
};

const init = () => {
    websocket.addActionListener('players', (players) => {
        let currentPlayerIds = Object.keys(playerMapIcons);
        let newPlayerIds = players.map(player => { return player.id });
        currentPlayerIds.filter(id => !newPlayerIds.includes(id)).forEach((id) => {
            if (playerMapIcons[id] === followingPlayer) {
                followPlayer(null);
            }
            playerMapIcons[id].playerListEntry.el.remove();
            map.removeIcon(playerMapIcons[id]);
            delete playerMapIcons[id];
        });

        players.forEach((player) => {
            let playerMapIcon = playerMapIcons[player.id];
            if (!playerMapIcon) {
                // new player
                const playerListEntry = createUi(`
					<div class="playerListEntry">
						<div class="name" data-id="name"></div>
						<div class="hpBar" data-id="hpBar">
							<div class="hp" data-id="hp"></div>
							<div class="hpText" data-id="hpText"></div>
						</div>
					</div>
				`);
                playerListEntry.ui.name.textContent = player.name;
                ui.playerList.appendChild(playerListEntry.el);
                playerMapIcon = {
                    ...player,
                    type: 'player',
                    text: player.name,
                    zIndex: 5,
                    playerListEntry
                };
                if (!player.hidden) {
                    map.addIcon(playerMapIcon, false);
                } else {
                    playerListEntry.ui.hpBar.style.display = 'none';
                }
                playerMapIcons[player.id] = playerMapIcon;
                playerListEntry.el.addEventListener('click', () => {
                    if (!playerMapIcon.hidden) {
                        if (ui.playerListTut) {
                            ui.playerListTut.remove();
                            ui.playerListTut = undefined;
                        }
                        followPlayer(playerMapIcon);
                    }
                });
            }

            if (!player.hidden && playerMapIcon.hidden) {
                // no longer hidden
                playerMapIcon.hidden = player.hidden;
                map.addIcon(playerMapIcon, false);
                playerMapIcon.playerListEntry.ui.hpBar.style.display = 'block';
            } else if (player.hidden && !playerMapIcon.hidden) {
                // becomming hidden
                playerMapIcon.hidden = player.hidden;
                map.removeIcon(playerMapIcon);
                playerMapIcon.playerListEntry.ui.hpBar.style.display = 'none';
                if (followingPlayer === playerMapIcon) {
                    followPlayer(null);
                }
            }

            playerMapIcon.lastUpdate = Date.now();
            playerMapIcon.x = player.x;
            playerMapIcon.z = player.z;

            if (!player.hidden) {
                playerMapIcon.playerListEntry.ui.hp.style.width = `${100 * Math.max(player.health / player.maxHealth, 0)
                    }%`;
                playerMapIcon.playerListEntry.ui.hpText.textContent = `${Math.round(Math.max(player.health, 0))
                    } / ${Math.round(player.maxHealth)
                    }`;

                map.explore(player.x, player.z);
            }
        });
        map.updateIcons();
    });
};

export default {
    init
};
