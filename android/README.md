# NothingMusic для Android

Мобильная версия NothingMusic: `music.youtube.com` в WebView, тот же
`audio-effects.js`, что и на десктопе, плюс foreground-сервис, чтобы музыка
играла при выключенном экране.

## Скачать

Готовый APK: **[NothingMusic 1.0.1 для Android](https://github.com/Dezigwer/NothingMusic/releases/tag/android-v1.0.1)**

APK универсальный — внутри нет нативных библиотек, поэтому один файл
подходит для `arm64-v8a`, `armeabi-v7a` и `x86_64`. Минимальная версия —
Android 8.0 (API 26). Сборка подписана релизным ключом (не debug).

Десктопное приложение лежит в корне репозитория и не затрагивается — Android
живёт отдельно в этой папке.

## Что уже есть

- Экран с YouTube Music без браузерной обвязки, монохромная рамка.
- Скорость 0.5x–2x с сохранением питча.
- Эквалайзер: бас / средние / верхние, −12…+12 дБ.
- «Под водой» с глубиной и реверберация.
- Эффекты применяются на лету, трек перезапускать не нужно.
- Настройки открываются оверлеем поверх плеера: музыка продолжает играть,
  пока крутится эквалайзер.
- Настройки сохраняются между запусками.
- Фоновое воспроизведение: foreground-сервис, wake lock + Wi-Fi lock,
  уведомление с play/pause, переключением треков и обложкой.
- Иконка приложения — та же нота, что у десктопной версии (adaptive icon,
  тема Pixelify Sans).
- Медиа-сессия: управление с экрана блокировки, часов и наушников.
- Назад: история WebView, а с главной — уход в фон (как трей на Windows).

## Сборка

### Через GitHub Actions (ничего ставить не нужно)

Workflow `.github/workflows/android.yml` собирает APK на каждый пуш в
`main`/`android` при изменениях в `android/`.

1. Откройте вкладку **Actions** → **Android APK** → последний запуск.
2. Скачайте артефакт **NothingMusic-release** (подписанный) или
   **NothingMusic-debug**.
3. Внутри `app-release.apk` / `app-debug.apk` — ставится на телефон как
   обычный APK (нужно разрешить установку из неизвестных источников).

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

Подпись включается автоматически, если в окружении есть `SIGNING_KEYSTORE`.
В CI ключ лежит в секретах:

| Секрет | Значение |
| --- | --- |
| `SIGNING_KEYSTORE_BASE64` | ключ (PKCS#12), закодированный в base64 |
| `SIGNING_STORE_PASSWORD` | пароль хранилища |
| `SIGNING_KEY_ALIAS` | alias ключа (`nothingmusic`) |
| `SIGNING_KEY_PASSWORD` | пароль ключа |

Workflow декодирует ключ в `release.p12`, собирает `assembleDebug
assembleRelease` и загружает оба APK. Без секретов release APK подпишется
debug-ключом, чтобы его всё равно можно было установить.

Локально:

```bash
SIGNING_KEYSTORE=release.p12 \
SIGNING_STORE_PASSWORD=… SIGNING_KEY_ALIAS=nothingmusic \
SIGNING_KEY_PASSWORD=… ./gradlew assembleRelease
```

Сам ключ и пароли в репозиторий не попадают — они в `.gitignore`.

## Структура

```
android/
  app/src/main/
    assets/audio-effects.js        # граф Web Audio (общий с десктопом)
    assets/player-bridge.js        # состояние плеера и команды для уведомления
    assets/fonts/PixelifySans.ttf
    java/com/dezigwer/nothingmusic/
      MainActivity.kt              # WebView + кнопка настроек
      SettingsPanel.kt            # настройки оверлеем поверх плеера
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
