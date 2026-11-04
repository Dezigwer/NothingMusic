# NothingMusic для Android

Мобильная версия NothingMusic: `music.youtube.com` в WebView, тот же
`audio-effects.js`, что и на десктопе, плюс foreground-сервис, чтобы музыка
играла при выключенном экране.

Десктопное приложение лежит в корне репозитория и не затрагивается — Android
живёт отдельно в этой папке.

## Что уже есть

- Экран с YouTube Music без браузерной обвязки, монохромная рамка.
- Скорость 0.5x–2x с сохранением питча.
- Эквалайзер: бас / средние / верхние, −12…+12 дБ.
- «Под водой» с глубиной и реверберация.
- Эффекты применяются на лету, трек перезапускать не нужно.
- Настройки сохраняются между запусками.
- Фоновое воспроизведение: foreground-сервис, wake lock, уведомление с
  play/pause, переключением треков и обложкой.
- Медиа-сессия: управление с экрана блокировки, часов и наушников.
- Назад: история WebView, а с главной — уход в фон (как трей на Windows).

## Сборка

### Через GitHub Actions (ничего ставить не нужно)

Workflow `.github/workflows/android.yml` собирает APK на каждый пуш в
`main`/`android` при изменениях в `android/`.

1. Откройте вкладку **Actions** → **Android APK** → последний запуск.
2. Скачайте артефакт **NothingMusic-debug**.
3. Внутри `app-debug.apk` — ставится на телефон как обычный APK
   (нужно разрешить установку из неизвестных источников).

### Локально

Нужны JDK 17+ и Android SDK 35 (проще всего — Android Studio).

```bash
cd android
./gradlew assembleDebug
# APK: app/build/outputs/apk/debug/app-debug.apk
```

На Windows:

```powershell
cd android
.\gradlew.bat assembleDebug
```

### Подписанный release

Добавьте в репозиторий секреты:

| Секрет | Значение |
| --- | --- |
| `SIGNING_KEYSTORE_BASE64` | keystore, закодированный в base64 |
| `SIGNING_STORE_PASSWORD` | пароль хранилища |
| `SIGNING_KEY_ALIAS` | alias ключа |
| `SIGNING_KEY_PASSWORD` | пароль ключа |

Затем поставьте тег `v1.0.0` — соберётся `NothingMusic-release`.
Без секретов release APK соберётся неподписанным.

## Структура

```
android/
  app/src/main/
    assets/audio-effects.js        # граф Web Audio (общий с десктопом)
    assets/player-bridge.js        # состояние плеера и команды для уведомления
    assets/fonts/PixelifySans.ttf
    java/com/dezigwer/nothingmusic/
      MainActivity.kt              # WebView + кнопка настроек
      SettingsActivity.kt          # экран настроек
      WebEngine.kt                 # один WebView на всё приложение
      PlaybackService.kt           # foreground-сервис, медиа-сессия, уведомление
      Settings.kt                  # настройки в SharedPreferences
```

## Отличия от десктопа

| Windows | Android |
| --- | --- |
| Трей | foreground-уведомление |
| Своя рамка окна | системные панели, чёрный фон |
| `%LOCALAPPDATA%\NothingMusic\settings.json` | SharedPreferences (`nothingmusic/settings`) |
| `AddScriptToExecuteOnDocumentCreated` | инжект на `onPageStarted` + `onPageFinished` |
| Размер окна запоминается | не нужно |

## Ограничения (важно)

1. **Вход в Google.** Google не любит OAuth внутри WebView и может показать
   «This browser or app may not be secure». UA приложения замаскирован под
   Chrome Mobile, но 100% гарантии нет. Если вход не проходит — попробуйте
   включить «Десктопную версию» в настройках. Гостевое прослушивание
   (поиск, радио) работает без входа.
2. **DRM.** Для обычного аудио YouTube Music Widevine не обязателен, но
   некоторые треки/видео могут не играть в WebView на отдельных устройствах.
3. **Эффекты и фон.** Web Audio в WebView может засыпать при выключенном
   экране. Приложение раз в секунду «будит» аудиоконтекст и держит wake lock,
   но на агрессивных прошивках (Xiaomi, Huawei и т.п.) стоит отключить
   оптимизацию батареи для NothingMusic.
4. **Кнопки next/prev** ищутся по `id` и `aria-label` в разметке YouTube.
   YouTube может её поменять — тогда переключение из уведомления перестанет
   работать, но воспроизведение и пауза (через `<video>`) останутся.
5. **Свайп из недавних** убивает процесс вместе с WebView — фоновая музыка
   остановится. Это ограничение архитектуры «WebView в приложении»; если
   нужно переживать свайп, WebView придётся переносить в сервис.
6. Приложение неофициальное и не связано с YouTube/Google. Использование
   может нарушать их условия — как и десктопная версия.
