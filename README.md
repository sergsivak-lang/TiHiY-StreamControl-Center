# TiHiY StreamControl Center Test v0.7.8

Тестова WPF-програма для Twitch, YouTube, Discord, OBS Audio та Donatello.

## Donatello API

Програма використовує офіційний API Token у заголовку `X-Token` та endpoints `/me`, `/donates`, `/subscribers`. Токен зберігається у Windows Credential Manager.

Нові події можуть одночасно з’являтися у панелі донатів, мультичаті, OBS Alerts overlay та окремому Discord-каналі монетизації.

## Discord

Є два незалежні списки каналів:
- початок Twitch/YouTube трансляцій;
- донати й платні підписки Donatello/Twitch/YouTube.

## OBS

У модулі Overlay доступні URL чату, Alerts і Now Playing.
