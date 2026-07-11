/* Wand RU - перехват и перевод имён читов в renderer. Сгенерировано build-hook.mjs, не править вручную. */
(function () {
  "use strict";
  if (typeof window === "undefined" || window.__wandRuCheatHook) return;
  window.__wandRuCheatHook = true;

  var DICT = {"_comment":"Словарь перевода имён читов Wand/WeMod. Приоритет: idioms(полная фраза) > words(слово/фраза) > suffixes(X Multiplier/Rate) > prefixes(Unlimited/No/Set/...) > passthrough. word = {n:им, g:род m|f|n|pl, gen?:род.падеж, acc?:вин.падеж}. Compound-имена (A / B) переводятся по частям. Новые читы - данными сюда, не в код.","idioms":{"god mode":"Режим бога","king age":"возраст короля","culture progress":"прогресс культуры","stealth mode":"Режим скрытности","one-hit kill":"Убийство с одного удара","one hit kill":"Убийство с одного удара","one-hit kills":"Убийство с одного удара","one hit kills":"Убийство с одного удара","one hit stun":"Оглушение с одного удара","one hit destroy objects":"Уничтожение объектов с одного удара","rapid fire":"Скорострельность","super speed":"Суперскорость","super jump":"Супер-прыжок","invisibility":"Невидимость","gravity":"Гравитация","stop time":"Остановить время","fly up":"Лететь вверх","fly down":"Лететь вниз","fly mode":"Режим полёта","teleport":"Телепорт","teleport to waypoint":"Телепорт к точке маршрута","teleport to saved location":"Телепорт к сохранённой позиции","teleport to marked location":"Телепорт к отмеченной позиции","teleport to surface":"Телепорт на поверхность","teleport to lifepod":"Телепорт к капсуле","teleport down":"Телепорт вниз","undo teleport":"Отменить телепорт","mark location":"Отметить позицию","save location":"Сохранить позицию","daytime +1 hour":"Время суток +1 час","immune to all negative status":"Иммунитет ко всем негативным эффектам","unlock all blueprint":"Открыть все чертежи","unlock all blueprints":"Открыть все чертежи","no bounty":"Без розыска","unlimited horse health":"Бесконечное здоровье лошади","unlimited horse energy":"Бесконечная энергия лошади","slower oxygen depletion":"Медленный расход кислорода","faster oxygen depletion":"Быстрый расход кислорода","slower stats depletion":"Медленный расход характеристик","faster stats depletion":"Быстрый расход характеристик","stamina regeneration rate":"Скорость восстановления выносливости","stamina consumption rate":"Скорость расхода выносливости","fullness consumption rate":"Скорость расхода сытости","hydration consumption rate":"Скорость расхода гидратации","rest consumption rate":"Скорость расхода отдыха","time pass speed":"Скорость течения времени","game time speed":"Скорость игрового времени","instant acceleration":"Мгновенное ускорение","auto reload":"Авто-перезарядка","kill everything":"Убить всех","toggle hud":"Переключить HUD","items don't decrease":"Предметы не убывают","no heat":"Без розыска","no police reinforcements":"Без подкреплений полиции","no police spotting":"Полиция не замечает","vehicle invincibility":"Неуязвимость машины","instant harvest":"Мгновенный сбор","take ownership":"Присвоить","no vehicle damage":"Без урона машины","no vehicle crashing":"Без аварий","invincible":"Неуязвимость","invincibility":"Неуязвимость","no clip":"Сквозь стены","noclip":"Сквозь стены","better accuracy":"Улучшенная точность"},"words":{"health":{"n":"здоровье","g":"n","gen":"здоровья"},"max health":{"n":"макс. здоровье","g":"n","gen":"макс. здоровья"},"hp":{"n":"HP","g":"n"},"fp":{"n":"FP","g":"n"},"money":{"n":"деньги","g":"pl","gen":"денег"},"cash":{"n":"наличные","g":"pl","gen":"наличных"},"gold":{"n":"золото","g":"n","gen":"золота","acc":"золото"},"prestige":{"n":"престиж","g":"m","gen":"престижа"},"renown":{"n":"известность","g":"f","gen":"известности","acc":"известность"},"dread":{"n":"ужас","g":"m","gen":"ужаса"},"piety":{"n":"благочестие","g":"n","gen":"благочестия"},"stress":{"n":"стресс","g":"m","gen":"стресса"},"prowess":{"n":"доблесть","g":"f","gen":"доблести","acc":"доблесть"},"diplomacy":{"n":"дипломатия","g":"f","gen":"дипломатии","acc":"дипломатию"},"intrigue":{"n":"интриги","g":"pl","gen":"интриг"},"stewardship":{"n":"управление","g":"n","gen":"управления"},"learning":{"n":"учёность","g":"f","gen":"учёности","acc":"учёность"},"martial":{"n":"военное дело","g":"n","gen":"военного дела"},"legitimacy":{"n":"легитимность","g":"f","gen":"легитимности","acc":"легитимность"},"stamina":{"n":"выносливость","g":"f","gen":"выносливости","acc":"выносливость"},"energy":{"n":"энергия","g":"f","gen":"энергии","acc":"энергию"},"focus":{"n":"концентрация","g":"f","gen":"концентрации","acc":"концентрацию"},"mana":{"n":"мана","g":"f","gen":"маны","acc":"ману"},"power":{"n":"сила","g":"f","gen":"силы","acc":"силу"},"deadeye":{"n":"меткий глаз","g":"m","gen":"меткого глаза","acc":"меткий глаз"},"reload":{"n":"перезарядка","g":"f","gen":"перезарядки","acc":"перезарядку"},"ammo":{"n":"патроны","g":"pl","gen":"патронов","acc":"патроны"},"recoil":{"n":"отдача","g":"f","gen":"отдачи","acc":"отдачу"},"grenades":{"n":"гранаты","g":"pl","gen":"гранат","acc":"гранаты"},"damage":{"n":"урон","g":"m","gen":"урона","acc":"урон"},"accuracy":{"n":"точность","g":"f","gen":"точности","acc":"точность"},"shield":{"n":"щит","g":"m","gen":"щита","acc":"щит"},"xp":{"n":"опыт","g":"m","gen":"опыта","acc":"опыт"},"exp":{"n":"опыт","g":"m","gen":"опыта","acc":"опыт"},"experience":{"n":"опыт","g":"m","gen":"опыта","acc":"опыт"},"skill points":{"n":"очки навыков","g":"pl","gen":"очков навыков","acc":"очки навыков"},"items":{"n":"предметы","g":"pl","gen":"предметов","acc":"предметы"},"weight":{"n":"вес","g":"m","gen":"веса","acc":"вес"},"carrying weight":{"n":"вес переноски","g":"m","gen":"веса переноски","acc":"вес переноски"},"max carrying weight":{"n":"макс. вес переноски","g":"m","gen":"макс. веса переноски"},"max weight":{"n":"макс. вес","g":"m","gen":"макс. веса"},"oxygen":{"n":"кислород","g":"m","gen":"кислорода","acc":"кислород"},"water":{"n":"вода","g":"f","gen":"воды","acc":"воду"},"food":{"n":"еда","g":"f","gen":"еды","acc":"еду"},"hunger":{"n":"голод","g":"m","gen":"голода","acc":"голод"},"thirst":{"n":"жажда","g":"f","gen":"жажды","acc":"жажду"},"radiation":{"n":"радиация","g":"f","gen":"радиации","acc":"радиацию"},"fall damage":{"n":"урон от падения","g":"m","gen":"урона от падения"},"crafting":{"n":"крафт","g":"m","gen":"крафта","acc":"крафт"},"crafting materials":{"n":"ресурсы крафта","g":"pl","gen":"ресурсов крафта","acc":"ресурсы крафта"},"crafting requirements":{"n":"требования крафта","g":"pl","gen":"требований крафта","acc":"требования крафта"},"building requirements":{"n":"требования постройки","g":"pl","gen":"требований постройки","acc":"требования постройки"},"construction":{"n":"строительство","g":"n","gen":"строительства","acc":"строительство"},"research":{"n":"исследования","g":"pl","gen":"исследований","acc":"исследования"},"resources":{"n":"ресурсы","g":"pl","gen":"ресурсов","acc":"ресурсы"},"wood":{"n":"дерево","g":"n","gen":"дерева","acc":"дерево"},"faith":{"n":"вера","g":"f","gen":"веры","acc":"веру"},"happiness":{"n":"счастье","g":"n","gen":"счастья","acc":"счастье"},"recruiting":{"n":"вербовка","g":"f","gen":"вербовки","acc":"вербовку"},"current population":{"n":"население","g":"n","gen":"населения","acc":"население"},"units":{"n":"юниты","g":"pl","gen":"юнитов","acc":"юниты"},"nitro":{"n":"нитро","g":"n","gen":"нитро","acc":"нитро"},"nanite clusters":{"n":"нанокластеры","g":"pl","gen":"нанокластеров","acc":"нанокластеры"},"stats":{"n":"характеристики","g":"pl","gen":"характеристик","acc":"характеристики"},"level":{"n":"уровень","g":"m","gen":"уровня","acc":"уровень"},"player level":{"n":"уровень игрока","g":"m","gen":"уровня игрока","acc":"уровень игрока"},"speed":{"n":"скорость","g":"f","gen":"скорости","acc":"скорость"},"game speed":{"n":"скорость игры","g":"f","gen":"скорости игры","acc":"скорость игры"},"player speed":{"n":"скорость игрока","g":"f","gen":"скорости игрока","acc":"скорость игрока"},"movement speed":{"n":"скорость передвижения","g":"f","gen":"скорости передвижения","acc":"скорость передвижения"},"attack speed":{"n":"скорость атаки","g":"f","gen":"скорости атаки","acc":"скорость атаки"},"jump height":{"n":"высота прыжка","g":"f","gen":"высоты прыжка","acc":"высоту прыжка"},"timer":{"n":"таймер","g":"m","gen":"таймера","acc":"таймер"},"challenge timer":{"n":"таймер испытания","g":"m","gen":"таймера испытания","acc":"таймер испытания"},"daytime":{"n":"время суток","g":"n","gen":"времени суток","acc":"время суток"},"skill cooldown":{"n":"перезарядка навыка","g":"f","gen":"перезарядки навыка","acc":"перезарядку навыка"},"teleport":{"n":"телепорт","g":"m","gen":"телепорта","acc":"телепорт"},"location":{"n":"позиция","g":"f","gen":"позиции","acc":"позицию"},"ship health":{"n":"здоровье корабля","g":"n","gen":"здоровья корабля"},"vehicle health":{"n":"здоровье машины","g":"n","gen":"здоровья машины"},"ai vehicles":{"n":"машины ИИ","g":"pl","gen":"машин ИИ","acc":"машины ИИ"},"hits":{"n":"удары","g":"pl","gen":"ударов","acc":"удары"},"kills":{"n":"убийства","g":"pl","gen":"убийств","acc":"убийства"},"defense":{"n":"защита","g":"f","gen":"защиты","acc":"защиту"},"durability":{"n":"прочность","g":"f","gen":"прочности","acc":"прочность"},"breath":{"n":"дыхание","g":"n","gen":"дыхания","acc":"дыхание"},"battery":{"n":"заряд","g":"m","gen":"заряда","acc":"заряд"},"temperature":{"n":"температура","g":"f","gen":"температуры","acc":"температуру"},"body temperature":{"n":"температура тела","g":"f","gen":"температуры тела","acc":"температуру тела"},"building":{"n":"строительство","g":"n","gen":"строительства","acc":"строительство"},"combo":{"n":"комбо","g":"n","gen":"комбо","acc":"комбо"},"eitr":{"n":"эйтр","g":"m","gen":"эйтра","acc":"эйтр"},"torpidity":{"n":"усталость","g":"f","gen":"усталости","acc":"усталость"},"swim speed":{"n":"скорость плавания","g":"f","gen":"скорости плавания","acc":"скорость плавания"},"move speed":{"n":"скорость передвижения","g":"f","gen":"скорости передвижения","acc":"скорость передвижения"},"walking speed":{"n":"скорость ходьбы","g":"f","gen":"скорости ходьбы","acc":"скорость ходьбы"},"tool energy":{"n":"энергия инструмента","g":"f","gen":"энергии инструмента","acc":"энергию инструмента"},"facility power":{"n":"энергия базы","g":"f","gen":"энергии базы","acc":"энергию базы"},"money value":{"n":"деньги","g":"pl","gen":"денег","acc":"деньги"},"time":{"n":"время","g":"n","gen":"времени","acc":"время"},"skills":{"n":"навыки","g":"pl","gen":"навыков","acc":"навыки"},"mission":{"n":"миссия","g":"f","gen":"миссии","acc":"миссию"},"skill cooldowns":{"n":"перезарядки навыков","g":"pl","gen":"перезарядок навыков","acc":"перезарядки навыков"},"attack cooldowns":{"n":"перезарядки атак","g":"pl","gen":"перезарядок атак","acc":"перезарядки атак"},"death penalty":{"n":"штраф за смерть","g":"m","gen":"штрафа за смерть","acc":"штраф за смерть"},"credits":{"n":"кредиты","g":"pl","gen":"кредитов","acc":"кредиты"},"score":{"n":"очки","g":"pl","gen":"очков","acc":"очки"},"heat":{"n":"розыск","g":"m","gen":"розыска","acc":"розыск"},"police":{"n":"полиция","g":"f","gen":"полиции","acc":"полицию"},"vehicle":{"n":"машина","g":"f","gen":"машины","acc":"машину"},"vehicle damage":{"n":"урон машины","g":"m","gen":"урона машины"},"boost":{"n":"ускорение","g":"n","gen":"ускорения","acc":"ускорение"},"nitro boost":{"n":"нитро-ускорение","g":"n","gen":"нитро-ускорения","acc":"нитро-ускорение"},"jetpack boost":{"n":"ускорение джетпака","g":"n","gen":"ускорения джетпака","acc":"ускорение джетпака"},"race timer":{"n":"таймер гонки","g":"m","gen":"таймера гонки","acc":"таймер гонки"},"weapon energy":{"n":"энергия оружия","g":"f","gen":"энергии оружия","acc":"энергию оружия"},"melee":{"n":"ближний бой","g":"m","gen":"ближнего боя","acc":"ближний бой"},"crafting speed":{"n":"скорость крафта","g":"f","gen":"скорости крафта","acc":"скорость крафта"},"drop rate":{"n":"шанс дропа","g":"m","gen":"шанса дропа","acc":"шанс дропа"},"critical chance":{"n":"шанс крита","g":"m","gen":"шанса крита","acc":"шанс крита"},"dino":{"n":"дино","g":"m","gen":"дино","acc":"дино"},"dinos":{"n":"дино","g":"pl","gen":"дино","acc":"дино"},"creature":{"n":"существо","g":"n","gen":"существа","acc":"существо"},"creatures":{"n":"существа","g":"pl","gen":"существ","acc":"существа"},"egg":{"n":"яйцо","g":"n","gen":"яйца","acc":"яйцо"},"engrams":{"n":"энграммы","g":"pl","gen":"энграмм","acc":"энграммы"},"tek engrams":{"n":"тек-энграммы","g":"pl","gen":"тек-энграмм","acc":"тек-энграммы"},"tribe":{"n":"племя","g":"n","gen":"племени","acc":"племя"},"structures":{"n":"постройки","g":"pl","gen":"построек","acc":"постройки"},"mutation":{"n":"мутация","g":"f","gen":"мутации","acc":"мутацию"},"harvest":{"n":"сбор","g":"m","gen":"сбора","acc":"сбор"},"fortitude":{"n":"стойкость","g":"f","gen":"стойкости","acc":"стойкость"},"imprint quality":{"n":"качество импринта","g":"n","gen":"качества импринта","acc":"качество импринта"},"creative mode":{"n":"творческий режим","g":"m","gen":"творческого режима","acc":"творческий режим"},"time of day":{"n":"время суток","g":"n","gen":"времени суток","acc":"время суток"},"gravity strength":{"n":"сила гравитации","g":"f","gen":"силы гравитации","acc":"силу гравитации"},"spaceship":{"n":"корабль","g":"m","gen":"корабля","acc":"корабль"},"ship":{"n":"корабль","g":"m","gen":"корабля","acc":"корабль"},"item":{"n":"предмет","g":"m","gen":"предмета","acc":"предмет"},"player":{"n":"игрок","g":"m","gen":"игрока","acc":"игрока"},"weapon":{"n":"оружие","g":"n","gen":"оружия","acc":"оружие"},"upgrade":{"n":"улучшение","g":"n","gen":"улучшения","acc":"улучшение"},"tokens":{"n":"жетоны","g":"pl","gen":"жетонов","acc":"жетоны"},"runes":{"n":"руны","g":"pl","gen":"рун","acc":"руны"},"rocket":{"n":"ракета","g":"f","gen":"ракеты","acc":"ракету"},"equipment":{"n":"снаряжение","g":"n","gen":"снаряжения","acc":"снаряжение"},"intel":{"n":"разведданные","g":"pl","gen":"разведданных","acc":"разведданные"},"house":{"n":"дом","g":"m","gen":"дома","acc":"дом"},"trees":{"n":"деревья","g":"pl","gen":"деревьев","acc":"деревья"},"overheat":{"n":"перегрев","g":"m","gen":"перегрева","acc":"перегрев"},"hour":{"n":"час","g":"m","gen":"часа","acc":"час"},"pal":{"n":"пал","g":"m","gen":"пала","acc":"пала"},"pals":{"n":"палы","g":"pl","gen":"палов","acc":"палов"},"cooldown":{"n":"перезарядка","g":"f","gen":"перезарядки","acc":"перезарядку"},"cooldowns":{"n":"перезарядки","g":"pl","gen":"перезарядок","acc":"перезарядки"},"points":{"n":"очки","g":"pl","gen":"очков","acc":"очки"},"sprint":{"n":"спринт","g":"m","gen":"спринта","acc":"спринт"},"reload speed":{"n":"скорость перезарядки","g":"f","gen":"скорости перезарядки","acc":"скорость перезарядки"},"attack":{"n":"атака","g":"f","gen":"атаки","acc":"атаку"},"skill":{"n":"навык","g":"m","gen":"навыка","acc":"навык"},"fuel":{"n":"топливо","g":"n","gen":"топлива","acc":"топливо"},"shields":{"n":"щиты","g":"pl","gen":"щитов","acc":"щиты"},"hull":{"n":"корпус","g":"m","gen":"корпуса","acc":"корпус"},"cargo":{"n":"груз","g":"m","gen":"груза","acc":"груз"},"nanites":{"n":"наниты","g":"pl","gen":"нанитов","acc":"наниты"},"wanted level":{"n":"уровень розыска","g":"m","gen":"уровня розыска","acc":"уровень розыска"},"enemy":{"n":"враг","g":"m","gen":"врага","acc":"врага"},"tech":{"n":"технологии","g":"pl","gen":"технологий","acc":"технологии"},"religion":{"n":"религия","g":"f","gen":"религии","acc":"религию"},"seeds":{"n":"семена","g":"pl","gen":"семян","acc":"семена"},"material":{"n":"материал","g":"m","gen":"материала","acc":"материал"},"materials":{"n":"материалы","g":"pl","gen":"материалов","acc":"материалы"},"production":{"n":"производство","g":"n","gen":"производства","acc":"производство"},"components":{"n":"компоненты","g":"pl","gen":"компонентов","acc":"компоненты"},"staff":{"n":"персонал","g":"m","gen":"персонала","acc":"персонал"},"tiles":{"n":"плитки","g":"pl","gen":"плиток","acc":"плитки"},"status":{"n":"статус","g":"m","gen":"статуса","acc":"статус"},"geo":{"n":"гео","g":"n","gen":"гео","acc":"гео"},"packs":{"n":"наборы","g":"pl","gen":"наборов","acc":"наборы"},"modifiers":{"n":"модификаторы","g":"pl","gen":"модификаторов","acc":"модификаторы"},"mutagen":{"n":"мутаген","g":"m","gen":"мутагена","acc":"мутаген"},"scale":{"n":"масштаб","g":"m","gen":"масштаба","acc":"масштаб"},"agency":{"n":"агентство","g":"n","gen":"агентства","acc":"агентство"},"jump":{"n":"прыжок","g":"m","gen":"прыжка","acc":"прыжок"},"death":{"n":"смерть","g":"f","gen":"смерти","acc":"смерть"},"meter":{"n":"счётчик","g":"m","gen":"счётчика","acc":"счётчик"},"stone":{"n":"камень","g":"m","gen":"камня","acc":"камень"},"population":{"n":"население","g":"n","gen":"населения","acc":"население"},"reputation":{"n":"репутация","g":"f","gen":"репутации","acc":"репутацию"},"pistol":{"n":"пистолет","g":"m","gen":"пистолета","acc":"пистолет"},"shotgun":{"n":"дробовик","g":"m","gen":"дробовика","acc":"дробовик"},"rifle":{"n":"винтовка","g":"f","gen":"винтовки","acc":"винтовку"},"movement":{"n":"движение","g":"n","gen":"движения","acc":"движение"},"comfort":{"n":"комфорт","g":"m","gen":"комфорта","acc":"комфорт"},"research points":{"n":"очки исследований","g":"pl","gen":"очков исследований","acc":"очки исследований"}},"suffixes":[{"match":"^(.+?)\\s+Multiplier$","template":"Множитель {0}"},{"match":"^(.+?)\\s+Consumption Rate$","template":"Скорость расхода {0}"},{"match":"^(.+?)\\s+Regeneration Rate$","template":"Скорость восстановления {0}"},{"match":"^(.+?)\\s+Drop Rate$","template":"Шанс выпадения {0}"},{"match":"^(.+?)\\s+Rate$","template":"Скорость {0}"},{"match":"^(.+?)\\s+Speed$","template":"Скорость {0}"},{"match":"^(.+?)\\s+Cooldowns$","template":"Перезарядки {0}"},{"match":"^(.+?)\\s+Cooldown$","template":"Перезарядка {0}"},{"match":"^(.+?)\\s+Duration$","template":"Длительность {0}"},{"match":"^(.+?)\\s+Amount$","template":"Количество {0}"},{"match":"^(.+?)\\s+Level$","template":"Уровень {0}"},{"match":"^(.+?)\\s+Points$","template":"Очки {0}"},{"match":"^(.+?)\\s+Size$","template":"Размер {0}"},{"match":"^(.+?)\\s+Chance$","template":"Шанс {0}"},{"match":"^(.+?)\\s+Damage$","template":"Урон {0}"},{"match":"^(.+?)\\s+Charges$","template":"Заряды {0}"},{"match":"^(.+?)\\s+Consumption$","template":"Расход {0}"},{"match":"^(.+?)\\s+Depletion$","template":"Расход {0}"},{"match":"^(.+?)\\s+Capacity$","template":"Вместимость {0}"},{"match":"^(.+?)\\s+Meter$","template":"Счётчик {0}"},{"match":"^(.+?)\\s+Count$","template":"Количество {0}"},{"match":"^(.+?)\\s+Cost$","template":"Стоимость {0}"},{"match":"^(.+?)\\s+Regen$","template":"Восстановление {0}"},{"match":"^(.+?)\\s+Ammo$","template":"Патроны {0}"}],"prefixes":[{"match":"^(?:unlimited|infinite)\\s+(.+)$","adj":{"m":"Бесконечный","f":"Бесконечная","n":"Бесконечное","pl":"Бесконечные"}},{"match":"^instant\\s+(.+)$","adj":{"m":"Мгновенный","f":"Мгновенная","n":"Мгновенное","pl":"Мгновенные"}},{"match":"^(?:super|mega)\\s+(.+)$","adj":{"m":"Супер","f":"Супер","n":"Супер","pl":"Супер"}},{"match":"^fast\\s+(.+)$","adj":{"m":"Быстрый","f":"Быстрая","n":"Быстрое","pl":"Быстрые"}},{"match":"^easy\\s+(.+)$","adj":{"m":"Лёгкий","f":"Лёгкая","n":"Лёгкое","pl":"Лёгкие"}},{"match":"^zero\\s+(.+)$","adj":{"m":"Нулевой","f":"Нулевая","n":"Нулевое","pl":"Нулевые"}},{"match":"^full\\s+(.+)$","adj":{"m":"Полный","f":"Полная","n":"Полное","pl":"Полные"}},{"match":"^free\\s+(.+)$","adj":{"m":"Бесплатный","f":"Бесплатная","n":"Бесплатное","pl":"Бесплатные"}},{"match":"^no\\s+(.+)$","form":"gen","template":"Без {0}"},{"match":"^max(?:imum)?\\s+(.+)$","template":"Макс. {0}"},{"match":"^min(?:imum)?\\s+(.+)$","template":"Мин. {0}"},{"match":"^set\\s+(.+)$","form":"acc","template":"Задать {0}"},{"match":"^edit\\s+(.+)$","form":"acc","template":"Изменить {0}"},{"match":"^refill\\s+(.+)$","form":"acc","template":"Пополнить {0}"},{"match":"^restore\\s+(.+)$","form":"acc","template":"Восстановить {0}"},{"match":"^add\\s+(.+)$","form":"acc","template":"Добавить {0}"},{"match":"^increase\\s+(.+)$","form":"acc","template":"Увеличить {0}"},{"match":"^decrease\\s+(.+)$","form":"acc","template":"Уменьшить {0}"},{"match":"^multiply\\s+(.+)$","form":"acc","template":"Умножить {0}"},{"match":"^reset\\s+(.+)$","form":"acc","template":"Сбросить {0}"},{"match":"^freeze\\s+(.+)$","form":"acc","template":"Заморозить {0}"},{"match":"^ignore\\s+(.+)$","form":"acc","template":"Игнорировать {0}"},{"match":"^enable\\s+(.+)$","form":"acc","template":"Включить {0}"},{"match":"^disable\\s+(.+)$","form":"acc","template":"Выключить {0}"},{"match":"^toggle\\s+(.+)$","form":"acc","template":"Переключить {0}"},{"match":"^save\\s+(.+)$","form":"acc","template":"Сохранить {0}"},{"match":"^undo\\s+(.+)$","form":"acc","template":"Отменить {0}"},{"match":"^unlock\\s+(.+)$","form":"acc","template":"Открыть {0}"},{"match":"^give\\s+(.+)$","form":"acc","template":"Выдать {0}"},{"match":"^spawn\\s+(.+)$","form":"acc","template":"Создать {0}"},{"match":"^kill\\s+(.+)$","form":"acc","template":"Убить {0}"},{"match":"^clear\\s+(.+)$","form":"acc","template":"Очистить {0}"},{"match":"^complete\\s+(.+)$","form":"acc","template":"Завершить {0}"},{"match":"^get\\s+(.+)$","form":"acc","template":"Получить {0}"},{"match":"^make\\s+(.+)$","form":"acc","template":"Сделать {0}"},{"match":"^find\\s+(.+)$","form":"acc","template":"Найти {0}"},{"match":"^take\\s+(.+)$","form":"acc","template":"Взять {0}"},{"match":"^hatch\\s+(.+)$","form":"acc","template":"Вылупить {0}"},{"match":"^force\\s+(.+)$","template":"Принудительно {0}"},{"match":"^better\\s+(.+)$","adj":{"m":"Улучшенный","f":"Улучшенная","n":"Улучшенное","pl":"Улучшенные"}},{"match":"^clone\\s+(.+)$","form":"acc","template":"Клонировать {0}"},{"match":"^all\\s+(.+)$","template":"Все {0}"},{"match":"^auto\\s+(.+)$","template":"Авто-{0}"}]};
  var GAMES = {"8":{"Unlimited Health":"Бесконечное здоровье","Unlimited Ammo":"Бесконечные патроны","Unlimited Grenades":"Бесконечные гранаты","No Reload":"Без перезарядки","One-Hit Kills":"Убийство с одного удара","No Recoil":"Без отдачи","Super Accuracy":"Суперточность","Rapid Fire":"Скорострельность","Multiple Primary Weapons":"Несколько единиц основного оружия","Super Jump":"Суперпрыжок","Jump Height":"Высота прыжка","Super Speed":"Суперскорость","Speed Scale":"Множитель скорости"},"49":{"Unlimited Health":"Бесконечное здоровье","Unlimited AP":"Бесконечные очки действия","No Radioactivity":"Без радиации","Freeze Settlement Size":"Заморозить размер поселения","Super Speed":"Суперскорость","Unlimited Weight":"Неограниченный переносимый вес","Unlimited Bottle Caps":"Бесконечные крышки","Unbreakable Pins":"Неломающиеся шпильки","Unlimited Attribute Points":"Бесконечные очки характеристик","Mega Exp":"Мегаопыт","Unlimited Ammo":"Бесконечные патроны","No Reload":"Без перезарядки","No Recoil":"Без отдачи","Easy Terminal Hack":"Лёгкий взлом терминалов","Free Crafting":"Бесплатный крафт","Fly Mode":"Режим полёта"},"92":{"God Mode":"Режим бога","Unlimited Naval Capacity":"Бесконечный лимит флота","Instant Movement":"Мгновенное перемещение","Instant Colony":"Мгновенная колонизация","Instant Survey":"Мгновенное обследование","Set Energy Credits":"Задать энергокредиты","Set Minerals":"Задать минералы","Set Food":"Задать еду","Set Alloys":"Задать сплавы","Set Valatile Moles":"Задать летучие пылинки","Set Goods":"Задать товары","Set Exoticc Gases":"Задать экзотические газы","Set Zro":"Задать зро","Set Nanites":"Задать наниты","Set Rare Crystals":"Задать редкие кристаллы","Set Dark Matter":"Задать тёмную материю","Set Living Metal":"Задать живой металл","Set Trade":"Задать торговую ценность","Super Max Resources Limit":"Супер-лимит ресурсов","Set Unity":"Задать единство","Set Influence":"Задать влияние","AI Accept Any Deal":"ИИ принимает любые сделки","Fast Construction":"Быстрое строительство","Fast Recruiting":"Быстрый набор войск","Fast Station Construction":"Быстрое строительство станций","Fast Research":"Быстрые исследования","Freeze Day":"Заморозить дату","Console/Dev Mode in Ironman":"Консоль/режим разработчика в «Железной воле»"},"115":{"Unlimited Health":"Бесконечное здоровье","Refill Health":"Восполнить здоровье","Unlimited Stamina":"Бесконечная выносливость","Refill Stamina":"Восполнить выносливость","Max Stamina":"Макс. выносливость","Easy Lock Picking":"Лёгкий взлом замков","Unlimited Throwables":"Бесконечные метательные предметы","Unlimited Items":"Бесконечные предметы","Free Crafting":"Бесплатный крафт","+5K Money":"+5000 денег","-5K Money":"-5000 денег","Set Money":"Задать деньги","Max Survivor Level XP":"Макс. опыт выживания","Max Agility Level XP":"Макс. опыт ловкости","Max Power Level XP":"Макс. опыт силы","Max Driver Level XP":"Макс. опыт вождения","Max Legend Level XP":"Макс. опыт легенды","Unlimited Durability":"Бесконечная прочность","Unlimited UV Flashlight":"Бесконечный УФ-фонарь","Unlimited Grappling Hook":"Бесконечный крюк-кошка","No Reload":"Без перезарядки","Add Selected Weapon Ammo":"Добавить патроны выбранному оружию","Super Accuracy":"Суперточность","No Recoil":"Без отдачи","Weapon Cloning (Zecman)":"Клонирование оружия (Zecman)","Unlimited Fuel":"Бесконечное топливо","One-Hit Kills":"Убийство с одного удара","Zombie Carnage":"Резня зомби","Set Damage Multiplier":"Задать множитель урона","Switch to Day":"Переключить на день","Switch to Night":"Переключить на ночь","Set Day Time":"Задать время суток","Freeze Timers":"Заморозить таймеры","Weapons/ Looting in Safe Zone":"Оружие и лут в безопасной зоне","Jump Height":"Высота прыжка","Teleport to Waypoint":"Телепорт к маркеру","Save Location":"Сохранить позицию","Teleport":"Телепорт"},"149":{"Set Command Power":"Задать очки командования","Unlimited Convoy":"Бесконечные конвои","Fast National Focus":"Быстрый национальный фокус","Fast Research":"Быстрые исследования","Unlimited Resources":"Бесконечные ресурсы","Super Production":"Суперпроизводство","Production Speed Multiplier":"Множитель скорости производства","Unlimited Organization":"Бесконечная организация","Unlimited Vehicles Fuel":"Бесконечное топливо техники","God Mode":"Режим бога","Fast Construction":"Быстрое строительство","Construction Speed Multiplier":"Множитель скорости строительства","Instant Movement":"Мгновенное перемещение","Enable Ironman Console":"Консоль в режиме «Железная воля»","Instant Agency Construction":"Мгновенное создание агентства","Instant Agency Upgrade":"Мгновенное улучшение агентства","Instant Agency Operatives":"Мгновенные оперативники агентства","Instant Intel Network":"Мгновенная разведсеть","Instant Intel Operations Prepare":"Мгновенная подготовка операций разведки","Instant Intel Operation Execute":"Мгновенное выполнение операции разведки","Unlimited Breakthroughs":"Бесконечные прорывы","Instant Prototype":"Мгновенный прототип","Instant Intel Decrypting":"Мгновенная расшифровка","Set Air Exp":"Задать опыт авиации","Set Navy Exp":"Задать опыт флота","Set Army Exp":"Задать опыт армии","Unlimited Nukes":"Бесконечные ядерные бомбы","Unlimited ManPower":"Бесконечные людские ресурсы","Set Political Power":"Задать политическую власть","No World Tension":"Без мировой напряжённости","Unlimited Stability":"Бесконечная стабильность","Low Occupation Resistance":"Низкое сопротивление оккупации","Instant War Goal":"Мгновенная цель войны","Fast Recruiting":"Быстрый набор войск"},"6158":{"Unlimited Health":"Бесконечное здоровье","Unlimited X Ray":"Бесконечный рентген","Easy Kills":"Лёгкие убийства","Unlimited Energy":"Бесконечная энергия","Zero Xray (Enemy)":"Нулевой рентген (враг)","Zero Energy (Enemy)":"Нулевая энергия (враг)"},"11167":{"Unlimited Health":"Бесконечное здоровье","Unlimited Ammo":"Бесконечные патроны","No Reload":"Без перезарядки","No Recoil":"Без отдачи","Better Accuracy":"Повышенная точность"},"33513":{"Unlimited Health":"Бесконечное здоровье","Unlimited Attack Meter":"Бесконечная шкала атаки","Unlimited Defense Meter":"Бесконечная шкала защиты","Weak Opponents":"Слабые противники","Unlimited Fatal Attacks":"Бесконечные смертельные удары","Unlimited Timer":"Бесконечный таймер","Free Crypt Boxes":"Бесплатные сундуки Крипты"},"45156":{"Freeze AI":"Заморозить ИИ"},"45323":{"God Mode":"Режим бога","Unlimited Vehicle Health":"Бесконечная прочность транспорта","No Overheat":"Без перегрева","Super Speed":"Суперскорость","Bullet Time":"Замедление времени","Unlimited Ammo -Gadget":"Бесконечный боезапас гаджетов","No Reload":"Без перезарядки","Better Accuracy":"Повышенная точность","No Recoil":"Без отдачи"},"45481":{"Unlimited Nitro":"Бесконечное нитро","Low Heat":"Низкий уровень розыска","Never Busted":"Никогда не поймают","Unlimited Time":"Бесконечное время","Always Win Race":"Всегда побеждать в гонке","Max SP":"Максимум SP"},"45495":{"Unlimited Nitro":"Бесконечное нитро","Freeze Timer":"Заморозить таймер","Unlimited Money":"Бесконечные деньги","Unlimited Parts Token":"Бесконечные жетоны запчастей","Freeze AI":"Заморозить ИИ"},"45978":{"Unlimited Health":"Бесконечное здоровье","Refill Health":"Восполнить здоровье","Health Regeneration Rate":"Скорость регенерации здоровья","Unlimited Stamina":"Бесконечная выносливость","Stamina Regeneration Rate":"Скорость регенерации выносливости","Stamina Consumption Rate %":"Скорость расхода выносливости, %","Unlimited RAM":"Бесконечное ОЗУ","Refill Ram":"Восполнить ОЗУ","RAM Regeneration Rate":"Скорость регенерации ОЗУ","RAM Consumption Rate %":"Скорость расхода ОЗУ, %","Stealth Mode":"Режим скрытности","Edit Max Carrying Weight":"Изменить макс. переносимый вес","Set Movement Speed":"Задать скорость передвижения","Super Jump":"Суперпрыжок","Unlimited Double Jump":"Бесконечный двойной прыжок","Defense Multiplier":"Множитель защиты","Unlimited Items/Ammo":"Бесконечные предметы / патроны","Items Won't Decrease":"Предметы не убывают","Healing Items No Cooldown":"Лечение без кулдауна","Grenades No Cooldown":"Гранаты без кулдауна","Projectile Launch System No Cooldown":"Система запуска снарядов без кулдауна","Edit Money":"Изменить деньги","Unlimited Components":"Бесконечные компоненты","Unlimited Quickhack Components":"Бесконечные компоненты скриптов","Unlimited XP":"Бесконечный опыт","XP Multiplier":"Множитель опыта","Unlimited Street Cred":"Бесконечная уличная репутация","Street Cred Multiplier":"Множитель уличной репутации","Max Skill Progression":"Макс. прокачка навыков","Skill Progression Multiplier":"Множитель прокачки навыков","Edit Attribute Points":"Изменить очки характеристик","Edit Perk Points":"Изменить очки перков","Edit Relic Points":"Изменить очки «Реликта»","Edit Player Level":"Изменить уровень игрока","Edit Street Cred Level":"Изменить уровень уличной репутации","Ignore Cyberware Capacity":"Игнорировать лимит киберимплантов","Edit Headhunter Skill Level":"Изменить уровень навыка «Хедхантер»","Edit Netrunner Skill Level":"Изменить уровень навыка «Нетраннер»","Edit Shinobi Skill Level":"Изменить уровень навыка «Синоби»","Edit Solo Skill Level":"Изменить уровень навыка «Соло»","Edit Engineer Skill Level":"Изменить уровень навыка «Техник»","No Reload":"Без перезарядки","Super Accuracy":"Суперточность","No Recoil":"Без отдачи","One-Hit Kills":"Убийство с одного удара","Damage Multiplier":"Множитель урона","Freeze Breach Protocol Timer":"Заморозить таймер взлома протокола","Freeze Daytime":"Заморозить время суток","Daytime +1 Hour":"Время суток +1 час","Set Game Speed":"Задать скорость игры","Enable Fly Mode":"Включить режим полёта","Set Fly Height":"Задать высоту полёта","Set Fly Speed":"Задать скорость полёта"},"47153":{"Set Money":"Задать деньги","Set Prestige":"Задать престиж","Set Piety":"Задать благочестие","Set Renown":"Задать известность","Set Dread":"Задать ужас","Set Diplomacy":"Задать дипломатию","Set Martial":"Задать военное дело","Set Stewardship":"Задать управление","Set Intrigue":"Задать интриги","Set Learning":"Задать учёность","Set Prowess":"Задать доблесть","Set King Age":"Задать возраст правителя","Set Stress":"Задать стресс","Instant Construction":"Мгновенное строительство","Construction Speed Multiplier":"Множитель скорости строительства","Fast Culture Progress":"Быстрый прогресс культуры","Instant Movement":"Мгновенное перемещение","Movement Speed Multiplier":"Множитель скорости передвижения","Max County Control":"Макс. контроль графства","Freeze Time":"Заморозить время","Enable Console":"Включить консоль"},"47496":{"Unlimited Health":"Бесконечное здоровье","Unlimited Stamina":"Бесконечная выносливость","Never Hungry":"Без голода","Never Thirsty":"Без жажды","No Poison":"Без отравления","Never Dirty":"Без загрязнения","No Intoxication":"Без опьянения","Unlimited Carry Weight":"Неограниченный переносимый вес","+10 Skill Points":"+10 очков навыков","+100 Skill Points":"+100 очков навыков","Reset Skill Points":"Сбросить очки навыков","Selected Item Amount":"Количество выбранного предмета","Set Item Amount":"Задать количество предметов","Amount to Add":"Количество для добавления","Add Amount to Selected Item":"Добавить количество к выбранному предмету","Repair Quick Slot Items":"Починить предметы в быстрых слотах","Increase Max Health by 25%":"Увеличить макс. здоровье на 25%","Max Health":"Макс. здоровье","Increase Max Stamina by 25%":"Увеличить макс. выносливость на 25%","Max Stamina":"Макс. выносливость","Increase Cold Protection by 25%":"Увеличить защиту от холода на 25%","Cold Protection":"Защита от холода","Increase Heat Protection by 25%":"Увеличить защиту от жары на 25%","Heat Protection":"Защита от жары","Add 100 Building Tech Levels":"+100 уровней технологии строительства","Building Tech Level":"Уровень технологии строительства","Add 100 Crafting Tech Levels":"+100 уровней технологии ремесла","Crafting Tech Level":"Уровень технологии ремесла","Add 100 Farming Tech Levels":"+100 уровней технологии земледелия","Farming Tech Level":"Уровень технологии земледелия","Add 100 Survival Tech Levels":"+100 уровней технологии выживания","Survival Tech Level":"Уровень технологии выживания","Comfortable Low Temperature":"Комфортная низкая температура","Comfortable High Temperature":"Комфортная высокая температура","Age":"Возраст","Overloaded Carry Weight":"Перегруз без штрафа","One-Hit Kills":"Убийство с одного удара","Free Crafting":"Бесплатный крафт","Free Building":"Бесплатное строительство","Instant Building":"Мгновенное строительство","Time - Hours":"Время - часы","Time - Minutes":"Время - минуты","Skip Day":"Пропустить день","Skip Season":"Пропустить сезон","Game Speed Multiplier":"Множитель скорости игры","Free Camera":"Свободная камера","Fly Mode":"Режим полёта","Fly Up":"Лететь вверх","Fly Down":"Лететь вниз","No Clip":"Проход сквозь стены","Camera FOV":"Поле зрения камеры","High Res Screenshot":"Скриншот в высоком разрешении","Super Speed":"Суперскорость","Player Speed Multiplier":"Множитель скорости игрока","Low Gravity":"Низкая гравитация","Gravity Scale":"Множитель гравитации","Super Jump":"Суперпрыжок","Jump Height Multiplier":"Множитель высоты прыжка","Teleport to Vendor":"Телепорт к торговцу","Teleport Height":"Высота телепорта","Teleport to Waypoint":"Телепорт к маркеру"},"59796":{"Unlimited Units Health":"Бесконечное здоровье отрядов","AI Accept All Deals":"ИИ принимает все сделки","Add 10 Supply Ships":"+10 кораблей снабжения","Disable Retooling":"Отключить переоснащение","Fast Battle":"Быстрые сражения","Fast Build":"Быстрое строительство","Fast Build Ships":"Быстрая постройка кораблей","Fast Civil Wars":"Быстрые гражданские войны","Fast Colonize":"Быстрая колонизация","Fast Enact":"Быстрое принятие законов","Fast Hire":"Быстрый найм","Fast Incorporate":"Быстрая инкорпорация","Fast Institutions":"Быстрые институты","Fast Interests":"Быстрые интересы","Fast Mobilize":"Быстрая мобилизация","Fast Movements":"Быстрые движения","Fast Research":"Быстрые исследования","Fast Revolution":"Быстрые революции","Fast Secret Goals":"Быстрые тайные цели","Fast Travels":"Быстрые перемещения","No Revolution":"Без революций","No Secession":"Без сецессий","No Shortages":"Без дефицита","Complete All Research":"Завершить все исследования","Set Gold Reserve":"Задать золотой запас","Set Investment Pool":"Задать инвестиционный фонд","Refil Gold Reserves":"Пополнить золотой запас","Reset Infamy":"Сбросить дурную славу"},"79700":{"Choose Team":"Выбор команды","Unhittable":"Враг промахивается","Invincible":"Неуязвимость","Infinite Super Meter":"Бесконечная шкала суперприёма","Infinite Kameo Meter":"Бесконечная шкала камео","One-Hit Kills":"Убийство с одного удара","Ignore Enemy Blocks":"Игнорировать блоки врага","Enemy Can't Use Kameos":"Враг не может использовать камео","No Enemy Super Meter":"Нет шкалы суперприёма у врага","No Enemy Kameo Meter":"Нет шкалы камео у врага","Infinite Fight Timer":"Бесконечный таймер боя"},"83036":{"Infinite Player Health":"Бесконечное здоровье игрока","Refill Health":"Восполнить здоровье","Set Health Regeneration Rate":"Задать скорость регенерации здоровья","Infinite Pal Health":"Бесконечное здоровье палов","Infinite Stamina":"Бесконечная выносливость","Infinite Satiety":"Бесконечная сытость","Refill Satiety":"Восполнить сытость","Set Player Satiety Decrease Rate":"Задать скорость убывания сытости","Temperature Always Normal":"Температура всегда нормальная","Set Stat Points":"Задать очки характеристик","Set Technology Points":"Задать очки технологий","Set Ancient Technology Points":"Задать очки древних технологий","No Item Weight":"Предметы без веса","Set Lifmunk Effigies":"Задать статуэтки лифмунка","[Sel. Item] Set Amount":"[Выбр. предмет] задать количество","Infinite Sanity":"Бесконечный рассудок","Set Experience Multiplier":"Задать множитель опыта","[Sel. Pal] Get Information":"[Выбр. пал] показать информацию","[Sel. Pal] Set Level":"[Выбр. пал] задать уровень","[Sel. Pal] Set Experience":"[Выбр. пал] задать опыт","[Sel. Pal] Set Rank":"[Выбр. пал] задать ранг","Infinite Weapon Durability":"Бесконечная прочность оружия","[Overheat Rifle] No Heat":"[Винтовка с перегревом] без нагрева","No Reload":"Без перезарядки","Set Loot Drop Multiplier":"Задать множитель добычи","[Player] Massive Work Speed":"[Игрок] огромная скорость работы","[All] Massive Work Speed":"[Все] огромная скорость работы","No Crafting Requirements":"Крафт без требований","No Building Requirements":"Строительство без требований","100% Capture Chance":"100% шанс поимки","Capture Change Multiplier":"Множитель шанса поимки","All Pals Are Rare":"Все палы редкие","Rare Pal Probability Multiplier":"Множитель вероятности редких палов","Instant Work Progress":"Мгновенный прогресс работы","Extra Work Progress":"Доп. прогресс работы","Infinite Torch Duration":"Бесконечный факел","Infinite Base Structure Health":"Бесконечная прочность построек базы","Everyone Can Be Captured":"Поймать можно любого","Ignore Building Overlapping":"Игнорировать пересечение построек","Stop Time":"Остановить время","Set Daytime Speed Rate":"Задать скорость дня","Set Nighttime Speed Rate":"Задать скорость ночи","Instant Fishing":"Мгновенная рыбалка","No Crime Reporting":"Преступления не замечают","Fish Speed [%]":"Скорость рыбы [%]","Pal Randomizer":"Рандомайзер палов","Level Randomizer":"Рандомайзер уровней","[Pal Level Randomizer] Set Min. Level":"[Рандомайзер уровней] мин. уровень","[Pal Level Randomizer] Set Max Level":"[Рандомайзер уровней] макс. уровень","Advance Time":"Время вперёд","Rewind Time":"Время назад","Instant Acceleration":"Мгновенное ускорение","Set Walking Speed Multiplier":"Задать множитель скорости ходьбы","Set Sprint Speed Multiplier":"Задать множитель скорости бега","Set Jump Height Multiplier":"Задать множитель высоты прыжка"},"97932":{"Unlimited Health":"Бесконечное здоровье","Unlimited Focus":"Бесконечная концентрация","Unlimited Stamina":"Бесконечная выносливость","Undetected":"Незаметность","No Fall Damage":"Без урона от падения","Unlimited Gadget":"Бесконечные гаджеты","Unlimited Clip":"Бесконечный магазин","Auto Reload":"Авто-перезарядка","Unlimited Ammo":"Бесконечные патроны","Better Accuracy":"Повышенная точность","Super Rate of Fire":"Суперскорострельность","Waypoint Teleport":"Телепорт к маркеру"},"98073":{"Unlimited Mental Energy":"Бесконечная ментальная энергия","Add Mental Energy":"Добавить ментальную энергию","Unlimited Elemental Slurry":"Бесконечная элементальная жижа","Add Elemental Slurry":"Добавить элементальную жижу","Unlimited Wealth":"Бесконечное богатство","Add Wealth":"Добавить богатство","Unlimited Nano Seed":"Бесконечные наносемена","Add NanoSeed":"Добавить наносемена","Unlimited Large Nano Seed":"Бесконечные большие наносемена","Add Large NanoSeed":"Добавить большие наносемена","Unlimited Stealth Nano Seed":"Бесконечные стелс-наносемена","Add Stealth NanoSeed":"Добавить стелс-наносемена","Unlimited Mysterious Box":"Бесконечные загадочные коробки","Add Mysterious Box":"Добавить загадочную коробку","Unlimited Biomulch":"Бесконечная биомульча","Add Biomulch":"Добавить биомульчу","Unlimited Heavy Metals":"Бесконечные тяжёлые металлы","Add Heavy Metals":"Добавить тяжёлые металлы","Unlimited Tungsten Scraps":"Бесконечный вольфрамовый лом","Add Tungsten Scraps":"Добавить вольфрамовый лом","Unlimited Filtered Water":"Бесконечная фильтрованная вода","Add Filtered Water":"Добавить фильтрованную воду","Game Speed":"Скорость игры"},"99970":{"Unlimited Health":"Бесконечное здоровье","Invisibility":"Невидимость","Clone Items When Clicking":"Клонировать предметы по клику","Unlimited Water for Watering Can":"Бесконечная вода в лейке","Unlimited Cash":"Бесконечные наличные","Edit Cash":"Изменить наличные","Unlimited Bank Balance":"Бесконечный банковский счёт","Edit Bank Balance":"Изменить банковский счёт","Instant Max NPC Relationship":"Мгновенно макс. отношения с NPC","Instant Plant Growth":"Мгновенный рост растений","Instant Ingredient Mixing":"Мгновенное смешивание ингредиентов","Unlock All Shop Items":"Открыть все товары в магазинах","No Curfew":"Без комендантского часа","No Investigate / No Body Search":"Без проверок / без обыска","Advance 1 Hour":"Вперёд на 1 час","Rewind 1 Hour":"Назад на 1 час","Time Stop":"Остановка времени","Run Speed Multiplier":"Множитель скорости бега"},"100599":{"Unlimited Studio Fund":"Бесконечный фонд студии","Edit Studio Fund":"Изменить фонд студии","Unlimited Cash":"Бесконечные наличные","Edit Cash":"Изменить наличные","Unlimited Influence Points":"Бесконечные очки влияния","Edit Influence Points":"Изменить очки влияния","Unlimited Reputation":"Бесконечная репутация","Edit Reputation":"Изменить репутацию","Multiply Progress Speed":"Множитель скорости прогресса","Multiply EXP Gain":"Множитель получаемого опыта","Max Staff Loyalty & Happiness":"Макс. лояльность и счастье персонала","Max Staff Professional Limit":"Макс. предел профессионализма персонала","Unlimited Water":"Бесконечная вода","Unlimited Electricity":"Бесконечное электричество","Mega Negotiation Bonus":"Мегабонус на переговорах","Game Speed":"Скорость игры"},"102569":{"Infinite Health":"Бесконечное здоровье","Defense Multiplier":"Множитель защиты","Infinite Barrier":"Бесконечный барьер","Infinite Energy":"Бесконечная энергия","Set Energy Regeneration":"Задать регенерацию энергии","Set Attack Speed":"Задать скорость атаки","Set Cast Speed":"Задать скорость каста","100% Critical Chance":"100% шанс крита","100% Drop Rate":"100% шанс выпадения","Edit Loot Drop Amount":"Изменить количество добычи","Set Player Speed":"Задать скорость игрока","Set Movement Speed":"Задать скорость передвижения","Edit Gold":"Изменить золото","Infinite Exp":"Бесконечный опыт","Exp Multiplier":"Множитель опыта","Edit Total Attribute Points":"Изменить очки характеристик","Edit Total Divinity Points":"Изменить очки божественности","Edit Total Active Points":"Изменить очки активных навыков","Edit Total Passive Points":"Изменить очки пассивных навыков","Set AI Speed":"Задать скорость ИИ","Super Damage/One-Hit Kills":"Суперурон / убийство с одного удара","Damage Multiplier":"Множитель урона","Set Game Speed":"Задать скорость игры","Save Location":"Сохранить позицию","Teleport":"Телепорт","Undo Teleport":"Отменить телепорт"},"108844":{"Units: Edit HP (Strength)":"Отряды: изменить HP (численность)","Units: Infinite Morale":"Отряды: бесконечный боевой дух","Units: Fast Move":"Отряды: быстрое перемещение","Fast Construction & Recruit":"Быстрое строительство и наём","Fast Research":"Быстрые исследования","Fast Reform":"Быстрые реформы","Edit Gold":"Изменить золото","Edit Stability":"Изменить стабильность","Edit Legitimacy":"Изменить легитимность","Edit Prestige":"Изменить престиж","Edit Diplomats":"Изменить дипломатов","Edit War Exhaustion":"Изменить усталость от войны","Edit Manpower":"Изменить людские ресурсы","Edit Sailors":"Изменить моряков","Edit Inflation":"Изменить инфляцию","Edit Army Tradition":"Изменить армейские традиции","Edit Navy Tradition":"Изменить флотские традиции","Edit Cultural Tradition":"Изменить культурные традиции","Edit Cultural Influence":"Изменить культурное влияние","Selected Character: Administrative Ability":"Выбранный персонаж: административный навык","Selected Character: Diplomatic Ability":"Выбранный персонаж: дипломатический навык","Selected Character: Military Ability":"Выбранный персонаж: военный навык","Religion Modifiers: Religious Influence":"Религия: религиозное влияние","Religion Modifiers: Karma":"Религия: карма","Religion Modifiers: Purity":"Религия: чистота","Religion Modifiers: Honor":"Религия: честь","Religion Modifiers: Rite Power":"Религия: сила обряда","Religion Modifiers: Righteousness":"Религия: праведность","Religion Modifiers: Harmony":"Религия: гармония","Religion Modifiers: Self Control":"Религия: самоконтроль","Religion Modifiers: Doom":"Религия: рок","AI Units: Drain HP":"Отряды ИИ: убывание HP","AI Units: Drain Morale":"Отряды ИИ: убывание боевого духа"},"115056":{"Unlimited Health":"Бесконечное здоровье","Max Health":"Макс. здоровье","Refill Health":"Восполнить здоровье","Set Armor":"Задать броню","Set Blunt Resistance":"Задать защиту от дробящего урона","Set Edge Resistance":"Задать защиту от режущего урона","Set Point Resistance":"Задать защиту от колющего урона","Set Fire Resistance":"Задать защиту от огня","Set Ice Resistance":"Задать защиту от льда","Set Energy Resistance":"Задать защиту от энергии","Set Wind Resistance":"Задать защиту от ветра","Set Falling Resistance":"Задать защиту от падений","Unlimited Mana":"Бесконечная мана","Refill Mana":"Восполнить ману","Max Mana":"Макс. мана","Unlimited Oxygen":"Бесконечный кислород","Oxygen Regeneration Multiplier":"Множитель регенерации кислорода","Add Ore Nuggets":"Добавить руду","Auto Lockpick":"Авто-взлом","Unlimited Lockpicks":"Бесконечные отмычки","Spawn Potions":"Создать зелья","Quantity":"Количество","Spawn Spells":"Создать заклинания","Spawn Materials":"Создать материалы","Spawn Amulets":"Создать амулеты","Spawn Rings":"Создать кольца","Spawn Magic Books":"Создать магические книги","Spawn Plants & Ingredients":"Создать растения и ингредиенты","Spawn Food":"Создать еду","Spawn Unique Items":"Создать уникальные предметы","Spawn 1H Swords":"Создать одноручные мечи","Spawn 2H Swords":"Создать двуручные мечи","Spawn Axes":"Создать топоры","Spawn Maces":"Создать булавы","Spawn Bows & Crossbows":"Создать луки и арбалеты","Spawn Scrolls":"Создать свитки","Spawn Keys":"Создать ключи","Spawn Armor":"Создать броню","Add Experience Points":"Добавить очки опыта","Add Learning Points":"Добавить очки обучения","Set Strength":"Задать силу","Set Dexterity":"Задать ловкость","One-Hit Kills":"Убийство с одного удара","Attack Speed":"Скорость атаки","Attack Speed Multiplier":"Множитель скорости атаки","Game Speed":"Скорость игры","Freeze Time":"Заморозить время","Set Time of Day":"Задать время суток","Fly Mode":"Режим полёта","Fly Speed":"Скорость полёта","Super Speed":"Суперскорость","Super Speed Multiplier":"Множитель суперскорости","Super Jump":"Суперпрыжок","Jump Height Multiplier":"Множитель высоты прыжка","Unlimited Jumps":"Бесконечные прыжки","Low Gravity":"Низкая гравитация","Gravity Strength":"Сила гравитации","Player Speed Multiplier":"Множитель скорости игрока","Reset Speed":"Сбросить скорость","Save Location":"Сохранить позицию","Teleport to Saved Location":"Телепорт к сохранённой позиции","Undo Teleport":"Отменить телепорт"},"116542":{"God Mode/Ignore Hits":"Режим бога / игнорировать попадания","Unlimited Health":"Бесконечное здоровье","Edit Max Health":"Изменить макс. здоровье","Defense Multiplier":"Множитель защиты","Unlimited Thrust":"Бесконечная тяга","Edit Max Thrust":"Изменить макс. тягу","Max Hacking Gauge":"Макс. шкала взлома","Unlimited Repair Cartridge":"Бесконечные ремонтные картриджи","Unlimited Ammo":"Бесконечные патроны","No Recoil":"Без отдачи","Rapid Fire":"Скорострельность","Edit Lunafilament":"Изменить лунафиламент","Lunafilament Multiplier":"Множитель лунафиламента","Edit Upgrade Components":"Изменить компоненты улучшений","Upgrade Components Multiplier":"Множитель компонентов улучшений","Edit Pure Lunum":"Изменить чистый лунум","Pure Lunum Multiplier":"Множитель чистого лунума","Edit Cabin Coins":"Изменить монеты кабины","Cabin Coins Multiplier":"Множитель монет кабины","Unlimited OPEN State Duration":"Бесконечная длительность режима OPEN","Super Hacking Damage":"Суперурон взлома","Hacking Damage Multiplier":"Множитель урона взлома","Super Damage/One-Hit Kills":"Суперурон / убийство с одного удара","Damage Multiplier":"Множитель урона","Freeze Puzzle Timer":"Заморозить таймер головоломки","Freeze Training Timer":"Заморозить таймер тренировки","Game Speed":"Скорость игры"},"117655":{"God Mode/Ignore Hits":"Режим бога / игнорировать попадания","Infinite Health":"Бесконечное здоровье","Defense Multiplier":"Множитель защиты","Infinite Oxygen":"Бесконечный кислород","Oxygen Drain Rate":"Скорость расхода кислорода","Infinite Ammo & Darts":"Бесконечные патроны и дротики","Infinite Remedy":"Бесконечные лекарства","Infinite Smoke Bombs":"Бесконечные дымовые бомбы","Stealth Mode":"Режим скрытности","Add Money":"Добавить деньги","Money Multiplier":"Множитель денег","Edit Selected Item Amount":"Изменить количество выбранного предмета","Ship: Infinite Health":"Корабль: бесконечная прочность","Ship: Defense Multiplier":"Корабль: множитель защиты","Ship: Infinite Crews":"Корабль: бесконечная команда","Ship: Infinite Ammo":"Корабль: бесконечный боезапас","Ship: Instant Weapon Cooldown":"Корабль: мгновенная перезарядка орудий","Ship: Clear Wanted Level":"Корабль: сбросить розыск","Ship: Super Damage/One-Hit Kills":"Корабль: суперурон / убийство с одного удара","Ship: Damage Multiplier":"Корабль: множитель урона","Easy Break Defense":"Лёгкий пробив защиты","Defense Gauge Damage Multiplier":"Множитель урона по шкале защиты","Super Damage/One-Hit Kills":"Суперурон / убийство с одного удара","Damage Multiplier":"Множитель урона","Freeze Time of Day":"Заморозить время суток","Daytime +1 Hour":"Время суток +1 час","Set Game Speed":"Задать скорость игры","Save Location":"Сохранить позицию","Teleport to Saved Location":"Телепорт к сохранённой позиции","Teleport to Marker Location":"Телепорт к отметке"},"117737":{"Unlimited HP":"Бесконечное HP","Ally Damage Multiplier":"Множитель урона союзников","Ally Cannot Die":"Союзники не умирают","One-Hit Kills":"Убийство с одного удара","Enemy Damage Multiplier":"Множитель урона врагов","Unlimited Money":"Бесконечные деньги","Money Gain Multiplier":"Множитель получения денег","Money Loss Multiplier":"Множитель потери денег","Unlimited Guard Endurance":"Бесконечная стойкость блока","Chain Count Multiplier":"Множитель счётчика цепочек","Unlimited Magicite":"Бесконечный магицит","Magicite Gain Multiplier":"Множитель получения магицита","Magicite Loss Multiplier":"Множитель потери магицита","Unlimited Arrows and Bombs":"Бесконечные стрелы и бомбы","Unlimited Item: Potions":"Бесконечные зелья","Unlimited Fairy Gauge":"Бесконечная шкала феи","Instant Cooldown Fairy Gauge":"Мгновенная перезарядка шкалы феи","Edit Money":"Изменить деньги","Edit Chain Count":"Изменить счётчик цепочек","Edit Player HP":"Изменить HP игрока"},"118911":{"Unlimited Health":"Бесконечное здоровье","Defense Multiplier":"Множитель защиты","Unlimited Instinct":"Бесконечный инстинкт","Unlimited Ammo":"Бесконечные патроны","No Reload":"Без перезарядки","Super Accuracy":"Суперточность","No Recoil":"Без отдачи","Stealth Mode":"Режим скрытности","Q-Lens: Unlimited Electricity":"Q-линзы: бесконечное электричество","Q-Lens: Unlimited Chemical":"Q-линзы: бесконечные химикаты","Super Damage/One-Hit Kills":"Суперурон / убийство с одного удара","Damage Multiplier":"Множитель урона","Game Speed":"Скорость игры"}};
  var TARGET_KEYS_ONLINE = new Set(["name", "displayName", "label"]);

  // CheatTranslator - движок перевода имён читов (Фаза 2, engine-first).
  // Чистые функции, без зависимостей. Тестируется node:test.
  // Рантайм-цель: renderer Wand (HAR: GET api.wemod.com/v3/games/{id}/trainer, Sec-Fetch-* => fetch/XHR).
  // Словарь (idioms/words/categories/prefixes/suffixes) передаётся аргументом; данные - cheat-dictionary.json.
  //
  // Приоритет резолва имени: compound-split(/ , & and) -> для каждой части:
  //   idiom(полная фраза) > word/phrase(полная фраза) > suffix(Multiplier/Rate) > prefix(Unlimited/No/Set/...)
  //   > passthrough(как есть).
  // Слово словаря: { n: им.падеж, g: род m|f|n|pl, gen?: род.падеж(«Без X»,«Множитель X»),
  //   acc?: вин.падеж(«Задать X»,«Изменить X»; нужен для жен. 1-го скл.: энергия->энергию) }.
  
  const TARGET_KEYS = new Set(["name", "displayName", "label"]);
  const CYRILLIC = /[А-Яа-яЁё]/;
  // Разбиение compound-имён: сохраняем разделители, чтобы собрать обратно (God Mode / Ignore Hits).
  const SPLIT = /(\s*\/\s*|\s*,\s*|\s*&\s*|\s+and\s+)/i;
  
  function hasKey(o, k) {
    return o && Object.prototype.hasOwnProperty.call(o, k);
  }
  
  const MAX_DEPTH = 5; // страховка от глубокой взаимной рекурсии resolveName<->resolveTail
  
  // Хвост (существительное после префикса) -> {nom, gen, acc, gender}. Не нашли - рекурсия в resolveName.
  function resolveTail(tail, dict, depth) {
    const t = tail.trim();
    const key = t.toLowerCase();
    if (hasKey(dict.idioms, key)) {
      return { nom: dict.idioms[key], gen: null, acc: null, gender: undefined };
    }
    if (hasKey(dict.words, key)) {
      const w = dict.words[key];
      return { nom: w.n, gen: w.gen || null, acc: w.acc || null, gender: w.g };
    }
    if (depth < MAX_DEPTH) {
      const inner = resolveName(t, dict, depth + 1); // вложенные префиксы: "Max HP", "X Multiplier"
      if (inner !== t) return { nom: inner, gen: null, acc: null, gender: undefined };
    }
    return { nom: t, gen: null, acc: null, gender: undefined }; // англ. как есть
  }
  
  // Одна часть имени (без compound-разделителей) -> строка перевода.
  function resolveName(seg, dict, depth = 0) {
    const s = seg.trim();
    if (s === "" || CYRILLIC.test(s)) return seg;
    const key = s.toLowerCase();
  
    if (hasKey(dict.idioms, key)) return dict.idioms[key];
    if (hasKey(dict.words, key)) return dict.words[key].n;
  
    // Тег в скобках: "[Spaceship] Unlimited Health" -> "[Корабль] Бесконечное здоровье".
    const br = s.match(/^\[([^\]]+)\]\s*(.+)$/);
    if (br && depth < MAX_DEPTH) {
      return "[" + resolveName(br[1], dict, depth + 1) + "] " + resolveName(br[2], dict, depth + 1);
    }
  
    // Prefix-паттерны (раньше suffix: "Set X Multiplier" = Set(X Multiplier), а не (Set X)Multiplier).
    for (const p of dict.prefixes || []) {
      const m = s.match(new RegExp(p.match, "i"));
      if (!m) continue;
      const r = resolveTail(m[1] !== undefined ? m[1] : "", dict, depth);
      if (p.adj) return p.adj[r.gender || "m"] + " " + r.nom; // прилагательное по роду
      if (p.form === "gen") return p.template.replace("{0}", r.gen || r.nom); // родительный
      if (p.form === "acc") return p.template.replace("{0}", r.acc || r.nom); // винительный
      return p.template.replace("{0}", r.nom); // им.падеж
    }
  
    // Suffix-паттерны (X Multiplier, X Rate): хвост в родительном.
    for (const suf of dict.suffixes || []) {
      const m = s.match(new RegExp(suf.match, "i"));
      if (!m) continue;
      const r = resolveTail(m[1] !== undefined ? m[1] : "", dict, depth);
      return suf.template.replace("{0}", r.gen || r.nom);
    }
    return seg; // ничего не подошло - оригинал
  }
  
  // Полное имя чита -> перевод. Идемпотентно (кириллица не трогается). Compound через разделители.
  // exact (опц.) - точный per-game map «имя -> перевод» (renderer/games/*.json), приоритет над движком.
  function translateText(str, dict, exact) {
    if (typeof str !== "string") return str;
    if (str.trim() === "" || CYRILLIC.test(str)) return str;
    if (exact && hasKey(exact, str.trim())) return exact[str.trim()];
    const res = str
      .split(SPLIT)
      .map((seg) => (SPLIT.test(seg) ? seg : resolveName(seg, dict)))
      .join("");
    // Капитализация первой КИРИЛЛИЧЕСКОЙ буквы (переведённый хвост строчный; латиницу/плейсхолдеры не трогаем).
    const i = res.search(/[а-яёА-ЯЁ]/);
    return i < 0 ? res : res.slice(0, i) + res.charAt(i).toLocaleUpperCase("ru") + res.slice(i + 1);
  }
  
  // Рекурсивный walker: новый объект, вход не мутирует. Переводит только имена (TARGET_KEYS).
  // ВАЖНО: category НЕ трогаем - это slug для ключа локали (trainer_cheats_list.category_<slug>),
  // его переводит локаль Фазы 1. Перевод slug ломает lookup ключа.
  function translateCheats(node, dict, exact) {
    if (Array.isArray(node)) return node.map((n) => translateCheats(n, dict, exact));
    if (node && typeof node === "object") {
      const out = {};
      for (const [k, v] of Object.entries(node)) {
        if (typeof v === "string" && TARGET_KEYS.has(k)) out[k] = translateText(v, dict, exact);
        else out[k] = translateCheats(v, dict, exact);
      }
      return out;
    }
    return node;
  }

  // Онлайн-MT добор для непокрытого офлайн-словарём (Фаза 3, путь A). Чистая логика с инъекцией
  // зависимостей (fs/https приходят снаружи - в хуке из require, в тестах моки). Провайдеры без ключа,
  // цепочкой: Google (gtx, качественнее) → MyMemory (фолбэк). Кэш en->ru. Всё опционально:
  // сбой/нет сети → офлайн-результат не трогаем.
  
  const LATIN = /[A-Za-z]/;
  
  // Уникальные англ. строки на целевых полях, которым нужен MT (офлайн не смог - осталась латиница).
  function collectUntranslated(node, targetKeys, out = new Set()) {
    if (Array.isArray(node)) {
      for (const n of node) collectUntranslated(n, targetKeys, out);
    } else if (node && typeof node === "object") {
      for (const [k, v] of Object.entries(node)) {
        if (typeof v === "string") {
          if (targetKeys.has(k) && LATIN.test(v)) out.add(v);
        } else collectUntranslated(v, targetKeys, out);
      }
    }
    return out;
  }
  
  // Применить map (оригинал->перевод) к целевым полям. Новый объект, вход не мутирует.
  function applyMap(node, map, targetKeys) {
    if (Array.isArray(node)) return node.map((n) => applyMap(n, map, targetKeys));
    if (node && typeof node === "object") {
      const out = {};
      for (const [k, v] of Object.entries(node)) {
        if (typeof v === "string") out[k] = targetKeys.has(k) && map[v] ? map[v] : v;
        else out[k] = applyMap(v, map, targetKeys);
      }
      return out;
    }
    return node;
  }
  
  function googleUrl(text) {
    return "https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=ru&dt=t&q=" +
      encodeURIComponent(text);
  }
  
  // Тело ответа gtx ([[["перевод","оригинал",...],...],...]) -> перевод или null.
  // Эхо (Google вернул исходный текст) считаем неудачей - не засорять кэш.
  function parseGoogle(body) {
    try {
      const j = JSON.parse(body);
      if (!Array.isArray(j) || !Array.isArray(j[0])) return null;
      const t = j[0].map((p) => (Array.isArray(p) ? p[0] : "")).filter(Boolean).join("");
      if (!t) return null;
      return t;
    } catch {
      return null;
    }
  }
  
  function myMemoryUrl(text) {
    return "https://api.mymemory.translated.net/get?q=" + encodeURIComponent(text) + "&langpair=en%7Cru";
  }
  
  // Тело ответа MyMemory -> перевод или null.
  function parseMyMemory(body) {
    try {
      const j = JSON.parse(body);
      const t = j && j.responseData && j.responseData.translatedText;
      // MyMemory при ошибке/лимите кладёт англ.-текст ошибки в translatedText - фильтруем очевидное.
      if (typeof t !== "string" || !t) return null;
      if (/MYMEMORY WARNING|QUERY LENGTH LIMIT|INVALID/i.test(t)) return null;
      return t;
    } catch {
      return null;
    }
  }
  
  // Перевести одну строку. provider: "auto" (Google -> фолбэк MyMemory, default), "google", "mymemory".
  // httpsGet: (url) => Promise<body>. Сбой -> null. Эхо-ответ (перевод == оригинал) не считаем переводом.
  async function translateOne(text, httpsGet, provider) {
    const p = provider || "auto";
    const useful = (t) => (t && t.trim().toLowerCase() !== text.trim().toLowerCase() ? t : null);
    if (p !== "mymemory") {
      try {
        const g = useful(parseGoogle(await httpsGet(googleUrl(text))));
        if (g) return g;
      } catch { /* провайдер упал - дальше фолбэк (в auto) */ }
      if (p === "google") return null;
    }
    try {
      return useful(parseMyMemory(await httpsGet(myMemoryUrl(text))));
    } catch {
      return null;
    }
  }
  
  // Перевод значений i18n.strings (описания/заметки читов). КЛЮЧИ не трогаем - по ним UI ищет
  // перевод; переводим только ЗНАЧЕНИЯ. Длинный текст офлайн не тянет - только MT + кэш.
  // Строки >1500 символов пропускаем (лимит URL у GET-провайдеров).
  async function translateStrings(map, deps) {
    const { cache, httpsGet, provider } = deps;
    if (!map || typeof map !== "object") return map;
    const out = {};
    await Promise.all(
      Object.entries(map).map(async ([k, v]) => {
        out[k] = v;
        if (typeof v !== "string" || !LATIN.test(v) || v.length > 1500) return;
        const ck = v.toLowerCase();
        if (ck in cache) { out[k] = cache[ck]; return; }
        const ru = await translateOne(v, httpsGet, provider);
        if (ru) { cache[ck] = ru; out[k] = ru; }
      })
    );
    return out;
  }
  
  // Оркестрация. node - ОРИГИНАЛЬНЫЙ (английский) ответ: MT всегда получает исходную строку,
  // а не полуфабрикат офлайна («Задать Prestige» ломает MT). deps.offline (опц.) - строковый
  // офлайн-переводчик: офлайн справился целиком (нет латиницы) -> MT не нужен; иначе MT по оригиналу,
  // при его сбое - хотя бы частичный офлайн. Без deps.offline поведение прежнее (MT по всем миссам).
  // deps: { cache (obj en_lower->ru, мутируется), httpsGet, targetKeys, provider?, offline? }.
  async function runOnline(node, deps) {
    const { cache, httpsGet, targetKeys, provider } = deps;
    const offline = deps.offline || ((s) => s);
    const all = [...collectUntranslated(node, targetKeys)]; // исходные англ. строки
    const map = {};
    const misses = [];
    for (const s of all) {
      const off = offline(s);
      if (!LATIN.test(off)) { map[s] = off; continue; } // офлайн перевёл целиком - MT не нужен
      if (s.toLowerCase() in cache) map[s] = cache[s.toLowerCase()];
      else misses.push(s);
    }
  
    await Promise.all(
      misses.map(async (s) => {
        const ru = await translateOne(s, httpsGet, provider);
        if (ru) { cache[s.toLowerCase()] = ru; map[s] = ru; } // кэшируем только успешные
        else { const off = offline(s); if (off !== s) map[s] = off; } // MT упал - частичный офлайн
      })
    );
  
    return applyMap(node, map, targetKeys);
  }

  var TRAINER = /\/v3\/games\/(\d+)\/trainer/;
  // Точный per-game словарь по gameId из URL (приоритет над движком).
  function exactFor(url) {
    var m = TRAINER.exec(url || "");
    return (m && GAMES[m[1]]) || null;
  }

  // --- Node-доступ (nodeIntegration:true у главного окна) для настроек/кэша/MT. Нет Node -> офлайн. ---
  var NODE = (typeof require === "function") ? require : null;
  function nodeDeps() {
    if (!NODE) return null;
    try {
      var fs = NODE("fs"), https = NODE("https"), p = NODE("path");
      var base = (typeof process !== "undefined" && process.env && process.env.APPDATA) || "";
      if (!base) return null;
      var dir = p.join(base, "WandRuInstaller");
      return { fs: fs, https: https, settings: p.join(dir, "settings.json"), cache: p.join(dir, "cheat-cache.json") };
    } catch (e) { return null; }
  }
  // Настройки онлайн-режима из settings.json: включён ли + провайдер (auto/google/mymemory).
  function onlineSettings(d) {
    try {
      var s = JSON.parse(d.fs.readFileSync(d.settings, "utf8")) || {};
      return {
        online: s.TranslateCheatsOnline === true,
        provider: (typeof s.OnlineProvider === "string" ? s.OnlineProvider : "auto").toLowerCase()
      };
    } catch (e) { return { online: false, provider: "auto" }; }
  }
  function loadCache(d) {
    try { return JSON.parse(d.fs.readFileSync(d.cache, "utf8")) || {}; } catch (e) { return {}; }
  }
  function saveCache(d, cache) {
    try { d.fs.writeFileSync(d.cache, JSON.stringify(cache), "utf8"); } catch (e) { /* не критично */ }
  }
  function httpsGetter(d) {
    return function (url) {
      return new Promise(function (resolve, reject) {
        var req = d.https.get(url, { timeout: 6000 }, function (r) {
          var body = ""; r.setEncoding("utf8");
          r.on("data", function (c) { body += c; });
          r.on("end", function () { resolve(body); });
        });
        req.on("error", reject);
        req.on("timeout", function () { req.destroy(new Error("timeout")); });
      });
    };
  }
  function withTimeout(promise, ms, fallback) {
    return new Promise(function (resolve) {
      var done = false;
      var t = setTimeout(function () { if (!done) { done = true; resolve(fallback); } }, ms);
      var fin = function (v) { if (!done) { done = true; clearTimeout(t); resolve(v); } };
      promise.then(fin, function () { fin(fallback); });
    });
  }

  // Офлайн-перевод (синхронно) - для XHR-пути. exact - точный per-game map (или null).
  function translateOffline(text, exact) {
    try { return JSON.stringify(translateCheats(JSON.parse(text), DICT, exact)); } catch (e) { return null; }
  }
  // Офлайн + (опц.) онлайн-MT добор - для fetch-пути. Возвращает Promise<string|null>.
  // ВАЖНО: runOnline получает ОРИГИНАЛЬНЫЙ ответ + строковый офлайн-переводчик - MT видит
  // исходный английский («Set Prestige»), а не полуфабрикат («Задать Prestige»).
  function translateAsync(text, exact) {
    var data;
    try { data = JSON.parse(text); } catch (e) { return Promise.resolve(null); }
    var offline = translateCheats(data, DICT, exact); // фолбэк и офлайн-путь
    var d = nodeDeps();
    var conf = d ? onlineSettings(d) : null;
    if (!d || !conf.online) return Promise.resolve(JSON.stringify(offline));
    try {
      var cache = loadCache(d);
      var deps = {
        cache: cache, httpsGet: httpsGetter(d), targetKeys: TARGET_KEYS_ONLINE,
        provider: conf.provider,
        offline: function (s) { return translateText(s, DICT, exact); }
      };
      // Имена читов + значения i18n.strings (описания/заметки; ключи не трогаем - по ним lookup).
      var combined = runOnline(data, deps).then(function (res) {
        if (!res || !res.i18n || !res.i18n.strings) return res;
        return translateStrings(res.i18n.strings, deps).then(function (s) {
          var i18n = {};
          for (var k in res.i18n) i18n[k] = res.i18n[k];
          i18n.strings = s;
          var out = {};
          for (var k2 in res) out[k2] = res[k2];
          out.i18n = i18n;
          return out;
        });
      });
      return withTimeout(combined, 12000, offline)
        .then(function (result) { saveCache(d, cache); return JSON.stringify(result); },
              function () { return JSON.stringify(offline); });
    } catch (e) { return Promise.resolve(JSON.stringify(offline)); }
  }

  // --- fetch (офлайн + онлайн-MT) ---
  var _fetch = window.fetch;
  if (typeof _fetch === "function") {
    window.fetch = function (input, init) {
      var url = typeof input === "string" ? input : (input && input.url) || "";
      var p = _fetch.apply(this, arguments);
      if (!TRAINER.test(url)) return p;
      return p.then(function (res) {
        try {
          if (!res || !res.ok) return res;
          var ct = (res.headers && res.headers.get("content-type")) || "";
          if (ct.indexOf("json") < 0) return res;
          return res.clone().text().then(function (text) {
            return translateAsync(text, exactFor(url)).then(function (t) {
              if (t == null) return res;
              var headers = new Headers(res.headers);
              headers.delete("content-length");
              return new Response(t, { status: res.status, statusText: res.statusText, headers: headers });
            });
          }).catch(function () { return res; });
        } catch (e) { return res; }
      });
    };
  }

  // --- XMLHttpRequest (офлайн-only, best-effort фолбэк; онлайн-MT требует async) ---
  var XP = window.XMLHttpRequest && window.XMLHttpRequest.prototype;
  if (XP && XP.open && XP.send) {
    var _open = XP.open, _send = XP.send;
    XP.open = function (method, url) { this.__wandRuUrl = url; return _open.apply(this, arguments); };
    XP.send = function () {
      var xhr = this;
      if (xhr.__wandRuUrl && TRAINER.test(xhr.__wandRuUrl)) {
        xhr.addEventListener("readystatechange", function () {
          if (xhr.readyState === 4 && xhr.status >= 200 && xhr.status < 300) {
            try {
              var t = translateOffline(xhr.responseText, exactFor(xhr.__wandRuUrl));
              if (t != null) {
                Object.defineProperty(xhr, "responseText", { value: t, configurable: true });
                Object.defineProperty(xhr, "response", { value: t, configurable: true });
              }
            } catch (e) { /* оставить как есть */ }
          }
        });
      }
      return _send.apply(this, arguments);
    };
  }
})();
