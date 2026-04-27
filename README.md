# Macro Master — макросы для tModLoader

Гибкая система макросов для Terraria 1.4.4 (tModLoader). Позволяет задать
до 24 настраиваемых клавиш и привязать к каждой собственный текстовый
макрос. Скрипты в стиле WoW: `/use`, `/cast`, `/buff`, `/wait`,
`/if … /endif`, `/while … /endwhile`, переменные, выражения, вызов
других макросов через `/run`.

Полностью совместим со всеми загруженными модами: ищет предметы и баффы
не только в ванилле, но и во всех модах (по внутреннему имени, имени
`ModName/InternalName`, отображаемому имени или числовому ID).

## Установка

1. Подпишись или собери мод:
   - `tModLoaderSteamPath=<путь_к_tModLoader> dotnet build -c Release MacroMod/MacroMod.csproj`
   - либо открой `MacroMod` в **Mod Sources** в самой игре и нажми **Build + Reload**.
2. Включи `Macro Master` в меню `Mods`.
3. Зайди в мир. По умолчанию открой панель макросов клавишей `M`,
   а `.` мгновенно останавливает все запущенные макросы.

## Где лежат макросы

`tModLoader/Macros/<Имя>.macro` (та же папка, что `Players`, `Worlds`).
Файлы — обычный текст, можно редактировать в любом редакторе.
Команда `/macro reload` или кнопка **Reload** в панели подхватит изменения,
а мод сам отслеживает изменения файлов раз в полсекунды.

## Чат-команды

| Команда | Что делает |
|---|---|
| `/macro list` | список всех макросов |
| `/macro create <name>` | создать новый пустой макрос |
| `/macro edit <name>` | открыть `.macro`-файл во внешнем редакторе |
| `/macro run <name>` | запустить макрос |
| `/macro stop` | остановить все запущенные макросы |
| `/macro reload` | перечитать макросы с диска |
| `/macro delete <name>` | удалить макрос |
| `/macro dir` | вывести путь к папке макросов |

## Назначение клавиш

Открой панель **Macro Master** (`M`), выбери макрос слева, кликни цифру
(1–24) внизу — макрос будет привязан к соответствующей клавише
из набора `Macro 1` … `Macro 24` (саму клавишу настраиваешь в
**Settings → Controls → Keybindings → Macro Master**).

## Язык макросов

Каждая строка — отдельная команда. Строки начинаются с `/`. Если строка
не начинается с `/`, она трактуется как `/say`. `#` или `//` в начале
строки — комментарий.

### Команды

```
/use <item>             использовать предмет (свапает в активный слот хотбара)
/cast <item>            синоним /use
/swap <item>            просто переключиться на предмет, не используя
/drop <item> [n]        выбросить из инвентаря
/buff <name> [сек]      повесить бафф (по имени/ID, в т.ч. модовые)
/debuff <name>          снять бафф
/wait <время>           пауза: 30 — тики; 1.5s — секунды; 200ms — мс; 1m — минуты
/say <текст>            написать в чат
/print <текст>          вывести в локальный чат
/run <name>             выполнить другой макрос (как функцию)
/stop                   завершить макрос
/loop                   перейти на начало (с условием в [..])
/quickheal /quickmana   ванильные «быстрые» действия Terraria
/quickbuff /mount       то же самое
/recall /mirror         использовать первое из: MagicMirror/IceMirror/Shellphone/CellPhone/RecallPotion
/if <выр.>              условный блок
/elseif <выр.>
/else
/endif
/while <выр.>           цикл
/endwhile
/set $var = <выр.>      присвоить переменную
```

### WoW-style модификаторы

Любую команду можно ограничить условием в квадратных скобках. Несколько
групп через пробел работают как «ИЛИ», запятая внутри группы — «И»,
`!` перед условием — отрицание.

```
/use [hp<50] HealingPotion             # пить хилку только когда HP меньше 50%
/cast [mod:shift,boss] BeesKnees       # альтернатива при удержании Shift и активном боссе
/use [!hasbuff:Regen] Regeneration_Potion
/recall [time:night, hostile]
```

Поддерживаются: `mod:shift`, `mod:ctrl`, `mod:alt`, `hp<X`/`hp>X`/`hp=X`
(в процентах), `mp<X` …, `hasbuff:Name`, `nobuff:Name`, `hasitem:Name`,
`noitem:Name`, `equipped:Name`, `boss`, `hostile`, `mounted`, `wet`,
`water`, `lava`, `honey`, `day`, `night`, `hardmode`, `expert`, `master`,
`underground`, `surface`, `time:day|night`, `moonphase:N`, `rand:0.25`,
`true`, `false`, а также любое произвольное выражение
(`/use [hppct() < 30 and boss()] HealingPotion`).

### Расширенный режим (для кодеров)

Можно использовать выражения — арифметика, сравнения, строки, вызовы
функций:

```
/set $threshold = 35
/while $threshold > 0
    /if [hppct() < $threshold]
        /use HealingPotion
        /wait 6s
    /endif
    /set $threshold = $threshold - 5
/endwhile
```

Доступные функции: `hp()`, `hpmax()`, `hppct()`, `mp()`, `mpmax()`,
`mppct()`, `defense()`, `time()`, `isday()`, `isnight()`, `hardmode()`,
`expert()`, `master()`, `moonphase()`, `rand()`/`rand(a,b)`, `mounted()`,
`wet()`, `water()`, `lava()`, `honey()`, `underground()`, `surface()`,
`boss()`, `hostile([radius])`, `buff("Name")`, `item("Name")`,
`itemcount("Name")`, `equipped("Name")`, `shift()`, `ctrl()`, `alt()`,
`min/max/abs/floor/ceil/round/len`. Внутри `/say` и `/print` строку можно
интерполировать выражениями: `/print HP={hppct()}%`.

### Поиск предметов и баффов

Имя ищется в таком порядке:

1. число — это ID;
2. `ModName/InternalName` — точное обращение к моду;
3. ванильное внутреннее имя (напр. `WoodenSword`);
4. внутреннее имя любого ModItem из загруженных модов;
5. отображаемое имя (как в подсказке предмета), без учёта регистра,
   пробелы можно заменить на `_`.

Так что для нелокализованных модов работает `/use Calamity/RoverDrive`,
а для всего остального просто `/use Wooden Sword`.

## Примеры

```macro
# Авто-хил ниже 50% HP, потом — мирор домой при низком ХП
/use [hp<50] HealingPotion
/wait 1s
/recall [hp<25]
```

```macro
# /castsequence из WoW: цикл оружия — каждый раз использует следующее
/run UseSeqWeapon
```

```macro
# Файл: UseSeqWeapon.macro
/if [equipped:Megashark]
    /swap "Star Cannon"
/elseif [equipped:Star_Cannon]
    /swap "Phoenix Blaster"
/else
    /swap "Megashark"
/endif
```

## Лицензия и происхождение

Структура мода скопирована со стандартного шаблона
[ExampleMod 1.4.4](https://github.com/tModLoader/tModLoader/tree/1.4.4/ExampleMod).
Весь остальной код — собственный.
