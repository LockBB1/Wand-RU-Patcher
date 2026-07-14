## Как собрать `WandRuInstaller.exe`

1. Нажмите **Fork** вверху страницы репозитория.
2. В своём форке откройте вкладку **Actions** и подтвердите включение workflow-ов
   (`I understand my workflows, go ahead and enable them`).
3. Слева выберите **Build executable** -> справа **Run workflow**.
4. В поле `variant` выберите сборку:
   - `classic` - работает «из коробки», ничего ставить не надо (~130 МБ);
   - `small` - компактная (~2.4 МБ), нужен [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0);
   - `both` - обе.
5. Дождитесь зелёной галочки (~3-5 мин), откройте запуск и скачайте артефакт из блока **Artifacts**.
6. Распакуйте zip - внутри `WandRuInstaller.exe`.

> Если Windows SmartScreen предупредит (`.exe` не подписан сертификатом) - «Подробнее» → «Выполнить в любом случае».

---

🔖 Обновляться вы будете отсюда же (`Sync fork` → `Run workflow`). ⭐ **Star** кладёт репозиторий в
ваше [избранное](https://github.com/stars) - закладка в один клик. 🔔 **Watch → Custom → Releases** -
и GitHub сам напишет о новой версии.
