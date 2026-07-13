# TiHiY StreamControl Center — Cyber Amber Full UI v3

Ця гілка створена **з функціональної бази v0.7.8**, а не з невдалого v0.9.0.

## Що змінено

- повністю новий `MainWindow.xaml` у затвердженому Cyber Amber стилі;
- глобальна система оформлення `Themes/CyberAmber.xaml` для всіх вікон;
- нові шаблони кнопок, полів, списків, ComboBox, CheckBox, Slider, ProgressBar, ScrollBar та DataGrid;
- новий логотип і Windows-іконка;
- функціональні обробники v0.7.8 не видалені;
- Twitch, YouTube, Discord, Donatello, OBS Audio, overlay і музичний модуль збережені;
- додано режим `--render-preview`, який рендерить **реальний WPF-скріншот** з поточної збірки;
- GitHub Actions збирає Windows-версію, запускає WPF renderer і додає фактичний скріншот до artifact.

## Перевірка через GitHub

1. Запустіть `UPLOAD-TO-GITHUB.bat`.
2. Відкрийте **GitHub → Actions**.
3. Оберіть workflow **Build Cyber Amber v3**.
4. Запустіть його для гілки `agent/cyber-amber-full-ui-v3`.
5. Після зеленого результату завантажте artifact `TiHiY-Cyber-Amber-v3-...`.
6. Усередині artifact будуть:
   - готова portable Windows-програма;
   - `Cyber-Amber-Actual.png` — фактичний WPF-рендер;
   - `Cyber-Amber-Approved.png` — затверджений еталон;
   - `BUILD-REPORT.txt`.

## Локальна перевірка

`VERIFY-CYBER-UI.bat` збирає self-contained win-x64 версію, запускає renderer і відкриває фактичний скріншот.

## Важливо

Цей пакет є вихідним кодом для реальної Windows-перевірки. Результат вважається прийнятим лише після зеленої GitHub Actions збірки та порівняння `Cyber-Amber-Actual.png` з еталоном.
