# Review bridge, 2026-09-18

Проверены исходник Plugin.cs и IL локальных DLL CFC, EpicLoot и Valheim.
Версии атрибутов BepInPlugin: CFC 4.0.3, EpicLoot 0.14.8.
SHA256 CFC из lib совпадает с установленной DLL:
`506B4D31E5E6A79951A824C681B05757B3054241136762A32FE998FBF959DD38`.
Название папки установленного CFC содержит 4.0.30, но версия самого плагина — 4.0.3.

По сообщению пользователя vanilla chests, RossItemDrawers 1.0.10, отображение,
списание и обычный сценарий LeaveOne работают в Valheim 1.0.14.
В рамках review игровые тесты не выполнялись; multiplayer остаётся непроверенным.

## Риски correctness и multiplayer

1. **Высокий: нет согласованного сетевого списания.** RemoveItem/RemoveExactItem
   меняют локальный Inventory без транзакции с владельцем контейнера. SaveContainer
   вызывает приватный Container.Save напрямую. В проверенной игре обычный
   Container.OnContainerChanged вызывает Save только после IsOwner(); сам Save
   сериализует m_inventory в ZDO без этой проверки. Прямой вызов обходит проверку
   владельца. Два клиента могут использовать устаревшие копии инвентаря: возможны
   потерянные изменения или повторное использование ресурса. Это риск, а не
   результат воспроизведения. Простого добавления ClaimOwnership недостаточно для
   доказательства атомарности; нужен отдельный разбор сетевого протокола.
2. **Высокий: успех списания не означает успех сохранения.** Перегрузка
   Inventory.RemoveItem(string, int, int, bool) возвращает void. Bridge без проверки
   фактического результата вычитает take из remaining. SaveContainer перехватывает
   ошибку, а callback всё равно возвращает успешное количество. При вмешательстве
   другого мода или ошибке сохранения это может расходиться с сохранённым состоянием.
   Точное списание проверяет bool, но также не подтверждает сетевое сохранение.
   Отката частично выполненной операции в bridge нет.
3. **Средний: неодинаковая семантика LeaveOne.** GetItems резервирует одну штуку
   на имя в контейнере; RemoveExactItem резервирует одну на сочетание имя/качество/
   worldLevel. Пример: один материал качества 1 и один качества 2, по одной штуке,
   LeaveOne=true. GetItems показывает один доступный вариант, но точное списание
   любого варианта возвращает 0. CountItem при допустимом worldLevel возвращает 1.
4. **Средний: несовпадающие фильтры callbacks.** GetItems и RemoveExactItem
   ограничиваются ItemType.Material. CountItem и RemoveItem по имени этого фильтра
   не имеют. CountItems(name, -1, true) и RemoveItem(name, ..., -1, true) исключают
   предметы с m_worldLevel ниже Game.m_worldLevel; GetItems не исключает их,
   RemoveExactItem сравнивает worldLevel с запрошенным предметом, а не с миром.
   Обычные материалы могут не выявлять эти расхождения; кастомные рецепты и
   смешанные варианты — выявлять.
5. **Средний: кэш CFC может устаревать.** В GetNearbyContainers ранний возврат
   cachedContainerList срабатывает при расстоянии от lastPosition меньше 0.5 м.
   Эта ветка не повторяет CheckAccess, IsInUse, PrivateArea.CheckAccess,
   AllowContainerType и проверку радиуса. Без инвалидации кэша другими путями
   изменение прав, занятости, позиции контейнера или настроек не обязано немедленно
   отразиться в bridge. Возвращение списка не является блокировкой контейнеров.
6. **Средний: поддержка discovery не гарантирует поддержку persistence.**
   Использование CFC.GetNearbyContainers и Container.GetInventory правильно
   сохраняет общий механизм поиска. Но виртуальный inventory должен корректно
   обслуживать обе перегрузки RemoveItem. Приватный Container.Save сериализует
   поле m_inventory, а не результат GetInventory. Совместимость custom container
   зависит от его перехватов операций и собственного сохранения. Успех RossItemDrawers
   не доказывает это для всех будущих модов.
7. **Низкий/условный: exact не сравнивает полную идентичность.** Клоны сопоставляются
   по имени, качеству и worldLevel, без prefab и customData. Для обычных материалов
   этого может хватать; варианты с разными данными и тем же именем могут быть
   перепутаны. Клонирование также теряет связь с конкретным исходным контейнером.
8. **Низкий/условный: ошибки настроек разрешают работу.** CfcIsActive при исключении
   возвращает true, LeaveOneEnabled — false. Это существующее поведение сохранено
   точечной правкой, но при сбое чтения можно проигнорировать запрет/резерв.
   Повторные исключения в callbacks могут засорять журнал.

## Наследование настроек CraftFromContainers 4.0.3

Имена взяты из установленного CFG и сопоставлены с IL CFC.

| Настройка | Фактическое поведение bridge |
|---|---|
| Enabled | Проверяется в CfcIsActive перед получением контейнеров. |
| PreventModKey, SwitchPrevent | Учитываются через CFC.AllowByKey(). |
| ContainerRange | Учитывается внутри GetNearbyContainers относительно позиции игрока; наследуется кэш CFC. |
| IgnoreShipContainers, IgnoreWagonContainers | Через AllowContainerType в discovery CFC; с ограничением кэша. |
| IgnoreWoodChests, IgnorePrivateChests, IgnoreBlackMetalChests, IgnoreReinforcedChests | Через AllowContainerType в discovery CFC; с ограничением кэша. |
| PullByDistance | CFC сортирует список при пересчёте, bridge обходит его в полученном порядке; с ограничением кэша. |
| LeaveOne | Значение читается, резерв реализован самим bridge. Для exact есть расхождение, описанное выше. |
| PullItemsKey | Не подключён к действиям EpicLoot: bridge не переносит ресурсы в инвентарь по этой клавише. |
| FillAllModKey | Не подключён; поведение наполнения топливом/рудой не относится к callbacks bridge. |
| FuelDisallowTypes, OreDisallowTypes | Не фильтруют материалы EpicLoot; применяются в сценариях топлива/руды CFC. |
| ResourceCostString, FlashColor, UnFlashColor | Bridge не применяет их к интерфейсу EpicLoot. |
| PulledMessage | Bridge не показывает сообщение CFC о переносе ресурсов. |
| ShowConnections, ConnectionStartOffset, ConnectionRemoveDelay | Не управляют bridge; визуальные связи остаются функцией самого CFC. |
| IsDebug | Не управляет логированием bridge. Вызванный код CFC сохраняет собственную настройку логов. |
| NexusID | Метаданные CFC; к bridge не применяются. |

CheckAccess, исключение IsInUse, проверка PrivateArea при m_checkGuardStone и
требование Piece/GetInventory также наследуются через discovery, но это не
отдельные настройки bridge и не гарантия актуальности при получении кэша.
Собственный поиск контейнеров не добавлялся.

## Выполненная точечная правка

- Убраны GetField/GetProperty/GetMethod/Invoke при чтении modEnabled, leaveOne
  и вызове AllowByKey. Их public-доступность подтверждена в DLL CFC 4.0.3.
- Регистрация провайдера возвращает bool; экземпляр снимает регистрацию только
  после собственного успешного Register. Это исключает использование статического
  MethodInfo как единственного признака успешной регистрации экземпляра.
- Reflection EpicLoot сохранён: пробная прямая ссылка вызвала MSB3274, потому что
  установленный EpicLoot 0.14.8 собран для net481, а bridge — net48. Целевой
  framework не повышался, проверка совместимости сборки не подавлялась.
- Reflection Container.Save сохранён: метод private. Замена на прямой вызов
  невозможна, а удаление принудительного сохранения меняет поведение и требует
  отдельного multiplayer/custom-container тестирования.
- Алгоритмы поиска, резервирования, удаления и сохранения не переписывались.
  Перечисленные риски этой правкой не устранены.

## Проверка

Release/net48 собран против установленной игры и точной CFC 4.0.3:

```powershell
dotnet build EpicLootCraftFromContainers/EpicLootCraftFromContainers.csproj -c Release --no-restore -p:ValheimDir=D:\Steam\steamapps\common\Valheim
```

Результат: 0 warnings, 0 errors. Игровая установка и архив релиза не обновлялись.
Для следующей проверки нужны смешанные качества/worldLevel с LeaveOne, два
клиента у одного сундука/ящика, открытый другим игроком контейнер и переподключение
после списания для проверки сохранённого количества.
