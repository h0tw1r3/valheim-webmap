import ui, { createUi } from "./ui";
import websocket from "./websocket";
import map from "./map";
import constants from "./constants";

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
                        <div class="details" data-id="details">
                            <div class="hpBar" data-id="hpBar">
                                <div class="hp" data-id="hp"></div>
                                <div class="hpText" data-id="hpText"></div>
                            </div>
                        </div>
                    </div>
                `);
                playerListEntry.ui.name.textContent = player.name;
                playerMapIcon = {
                    ...player,
                    type: 'player',
                    text: player.name,
                    zIndex: 5,
                    playerListEntry
                };
                map.addIcon(playerMapIcon, false);
                if (player.hidden && !constants.ALWAYS_VISIBLE) {
                    playerListEntry.ui.details.style.display = 'none';
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

                ui.playerList.appendChild(playerListEntry.el);
            }

            if ((constants.ALWAYS_VISIBLE || !player.hidden) && playerMapIcon.hidden) {
                // no longer hidden
		        map.showIcon(playerMapIcon);
                playerMapIcon.playerListEntry.ui.details.style.display = 'block';
            } else if (!constants.ALWAYS_VISIBLE && (player.hidden && !playerMapIcon.hidden)) {
                // becomming hidden
                map.hideIcon(playerMapIcon);
                playerMapIcon.playerListEntry.ui.details.style.display = 'none';
                if (followingPlayer === playerMapIcon) {
                    followPlayer(null);
                }
            }

            playerMapIcon.lastUpdate = Date.now();
            playerMapIcon.x = player.x;
            playerMapIcon.z = player.z;
            playerMapIcon.flags = player.flags;
            console.log(player.flags);
            console.log(playerMapIcon.flags);

            playerMapIcon.playerListEntry.ui.hp.style.width = `${100 * Math.max(player.health / player.maxHealth, 0) }%`;
            playerMapIcon.playerListEntry.ui.hpText.textContent = `${Math.round(Math.max(player.health, 0)) } / ${Math.round(player.maxHealth) }`;

            if (!player.hidden || constants.ALWAYS_MAP || constants.ALWAYS_VISIBLE) {
                map.explore(player.x, player.z);
            }
        });
        map.updateIcons();
    });
};

export default {
    init
};
