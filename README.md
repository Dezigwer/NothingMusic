# NothingMusic

![NothingMusic](hero.gif)

## Что внутри

- Своё окно вместо вкладки, работает в трее: закрытие прячет окно, полный выход из меню иконки
- Заголовок свой или системный, тёмная рамка, название окна можно поменять
- Скорость 0.5x-2x с сохранением питча, эквалайзер, «под водой», реверберация
- Размер окна и настройки запоминаются между запусками

## Звук

Настройки открываются кнопкой в заголовке, по `Ctrl+,`, правым кликом по заголовку или из меню трея. Эффекты применяются на ходу, трек перезапускать не нужно.

Скорость, сохранение питча, три полосы эквалайзера (-12..+12 дБ), «под водой» с глубиной, реверберация.

Настройки лежат в `%LOCALAPPDATA%\NothingMusic\settings.json`.

## Запуск

Скачать `NothingMusic.exe` из Releases и запустить. .NET ставить не нужно, нужен только WebView2 Runtime (в Windows 10/11 обычно уже есть).

Собрать самому:

```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## Android

В папке [`android/`](android/) — мобильная версия: тот же `audio-effects.js`,
WebView с YouTube Music, foreground-сервис и уведомление вместо трея.
APK собирается через GitHub Actions и лежит в
[Releases](https://github.com/Dezigwer/NothingMusic/releases/tag/android-v1.0.0)
(универсальный, Android 8+).

Подробности, сборка и ограничения: [`android/README.md`](android/README.md).
