# MilkDeadLockDry: маршрут от backend .NET к Avalonia

Решение на 8 октября 2026: локальный монолит, Avalonia/XAML,
CommunityToolkit.Mvvm, .NET 10. RabbitMQ и Kafka для этого приложения не нужны.
Это проект архитектуры и учебный маршрут; совместимость Deadlock с выбранным
Wine/графическим слоем ещё не подтверждена. Начинать с XAML с нуля нормально:
новая часть здесь — модель desktop UI, а не повторное изучение C#.

## Что строим

Один `MilkDeadLockDry.app`, три основных проекта:

- `MilkDeadLockDry.Desktop`: `App.axaml`, Views, ViewModels, DI, диалоги и навигация.
- `MilkDeadLockDry.Core`: сценарии и модели; функциональные области GameLaunch,
  Runtime, Mods, Profiles. Здесь нет ссылок на Avalonia или Toolkit.
- `MilkDeadLockDry.Infrastructure`: реализации контрактов для процессов Wine,
  JSON, VPK, загрузок, журналов восстановления и файловой системы macOS.

`MilkDeadLockDry.Cli` и проект тестов — вспомогательные инструменты. Три библиотеки
являются слоями; модульность возникает из границ функциональных областей,
их публичных операций и правил зависимостей. На первом этапе области могут
быть папками: отдельная сборка для каждого модуля пока не нужна.

Desktop зависит от Core; Infrastructure реализует контракты Core. Desktop
знает Infrastructure только при создании и регистрации сервисов. CLI использует
то же ядро. Игра и Steam — внешние процессы, не микросервисы лаунчера.

## Обязательные требования к архитектуре и коду

Весь код проекта пишем с соблюдением Clean Architecture, SOLID, DRY и KISS.
Применимые best practices .NET и Avalonia входят в критерии готовности каждого
этапа. Основой для C# служат официальные рекомендации Microsoft; конкретные
настройки проекта фиксируем в `.editorconfig` и `Directory.Build.props`.
Это требования к реализации и ревью будущего кода; конфигурационные файлы
и CI в этом архитектурном плане ещё не созданы.

### Clean Architecture

- Зависимости направлены к Core: Desktop → Core, Infrastructure → Core.
  Desktop → Infrastructure допускается в composition root для регистрации DI.
- Внутри Core разделяем `Domain` (модели и правила) и `Application` (сценарии
  и контракты инфраструктуры). Domain не зависит от Application; оба остаются
  в одной сборке, пока отдельные проекты не дают практической пользы.
- Core не знает Avalonia, Toolkit, Wine API, JSON-сериализацию или провайдер БД.
  Контракты границ используют модели Core, а детали реализации — Infrastructure.
- ViewModel управляет состоянием экрана и вызывает сценарии. Правила модов,
  запуск процессов и восстановление файлов имеют отдельные ответственности.

### SOLID, DRY и KISS

- **SRP:** у класса одна связная ответственность: запуск игры, расчёт плана
  модов, сохранение профиля или состояние экрана.
- **OCP:** другой runtime или хранилище подключается реализацией существующего
  контракта, когда его поведение соответствует этому контракту.
- **LSP:** FakeGameRunner и WineGameRunner соблюдают одинаковые обещания
  IGameRunner о результате, ошибках, отмене и владении процессами.
- **ISP:** небольшие интерфейсы по потребностям сценария, например IGameRunner
  и IProfileRepository; клиент не зависит от ненужных ему операций.
- **DIP:** сценарии зависят от контрактов Core; зависимости явно передаются
  через конструктор, реализации выбираются в composition root.
- **DRY:** у каждого бизнес-правила один источник. Повторяющееся знание
  выносим после выявления общей ответственности; похожий синтаксис сам по себе
  не требует универсальной абстракции.
- **KISS / YAGNI:** выбираем простое решение текущей задачи. Новая абстракция,
  слой, пакет или служба требует конкретного обоснования. Три основных проекта,
  прямые вызовы сценариев и JSON остаются стартовым решением; интерфейсы вводим
  на значимых границах, где нужна подмена реализации или изоляция внешнего I/O.

### C# code style по рекомендациям Microsoft

- Содержательные имена: PascalCase для типов и публичных членов, `I` для
  интерфейсов, camelCase для параметров и локальных переменных, `_camelCase`
  для закрытых полей экземпляра, суффикс `Async` для асинхронных методов.
- Четыре пробела, скобки Allman, file-scoped namespaces и `using` вне namespace.
  `var` используем, когда тип понятен из выражения; стиль одинаков во всех проектах.
- Nullable reference types и .NET analyzers включены. Область видимости минимальна;
  ресурсы и подписки освобождаются через `using`/`Dispose` и явного владельца.
- Для C# и XAML/AXAML принимаем проектный предел 120 символов и переносим длинные
  bindings/атрибуты с учётом вертикальной границы редактора и подсказок Rider.
  Это настройка нашего проекта, а не универсальное требование Microsoft.
- Комментарии объясняют причины и ограничения; XML-документация описывает
  публичные контракты, если их условия неочевидны из сигнатуры.

### Best practices и проверка готовности

- Асинхронное I/O, явная отмена долгих операций и доставка изменений UI в его
  поток. Ошибки обрабатываем на подходящей границе, сохраняем диагностику;
  отмена подготовки, сбой и остановка игры имеют разные результаты.
- Внешние данные проверяем: пути архивов/VPK, версии профилей и конфликты.
  Файловые изменения выполняем с блокировкой, staging и журналом восстановления.
- Проверки покрывают поведение и риски: конфликт, повторный запуск, отмену,
  сбой между заменами файлов и восстановление. Ревью проверяет границы слоёв.
- При создании solution добавляем общий `.editorconfig`, `Directory.Build.props`
  и фиксируем SDK. В CI включаем `dotnet format --verify-no-changes`, сборку,
  analyzers и необходимые тесты. Предупреждения исправляем; локальное подавление
  допускается с объяснением причины. Успешная сборка не заменяет ревью архитектуры.
- Этап завершён, когда выполнен его функциональный критерий, соблюдены правила
  этого раздела и пройдены относящиеся к изменению проверки. Обоснованные
  отклонения фиксируем в короткой заметке о решении.

Официальные материалы:

- [Microsoft: Clean Architecture](https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures#clean-architecture).
  Примеры относятся к web; правило зависимостей применяем к desktop-лаунчеру.
- [Microsoft: architectural principles / SRP / DIP / DRY](https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/architectural-principles).
- [Microsoft: C# coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions).
- [Microsoft: identifier naming](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/identifier-names).
- [Microsoft: EditorConfig и analyzers](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-files).

## Как понимать MVVM

`View` — `.axaml`: разметка окна, layout, bindings. `ViewModel` — состояние
экрана и команды: выбранный профиль, прогресс, статус, доступность кнопок.
`Model` — данные и правила приложения: ModProfile, RuntimeSpec, InstallPlan.

Нажатие кнопки вызывает команду ViewModel; команда ожидает сценарий Core.
Сценарий использует инфраструктуру через интерфейсы и сообщает прогресс.
View обновляется через binding и уведомления `INotifyPropertyChanged`.
Правила конфликтов модов и восстановления файлов не помещаем в ViewModel.
Code-behind допустим для поведения самого UI: focus, события окна, визуальные
взаимодействия. Превращать каждое такое действие в сервис не требуется.

## 0. Проверить главный риск параллельно с обучением UI

Практика: записать воспроизводимую конфигурацию Mac/macOS, Rosetta, Wine,
renderer и prefix. Через CLI запустить Windows Steam и Deadlock, собрать логи,
проверить вход, полноценный матч, ввод, звук и повторный запуск. Процесс игры
отслеживать отдельно от процесса Steam. Изменения файлов запрещать при работе
игры; операции записи сериализовать и защищать также от второго экземпляра
лаунчера, а не только отключением кнопки.

Готово: есть проверенный запуск игры и запись конфигурации/результатов. Пока
этого нет, UI можно изучать с FakeGameRunner; выпуск лаунчера остаётся за этим
контрольным пунктом. Не проверять совместимость только появлением главного меню.

## 1. Запустить первое окно

Читать: [Getting started](https://docs.avaloniaui.net/docs/get-started),
[создание проекта](https://docs.avaloniaui.net/docs/get-started/create-your-first-project).
В Rider проверить live XAML previewer. Сначала сделать отдельный учебный проект:

```sh
dotnet --list-sdks
dotnet new install Avalonia.Templates
dotnet new avalonia.mvvm -o MilkDeadLockDry.Lab -f net10.0 --mvvm CommunityToolkit
dotnet run --project MilkDeadLockDry.Lab
```

Параметры сверены с текущим исходником шаблона
[Avalonia.Templates](https://github.com/AvaloniaUI/avalonia-dotnet-templates/blob/master/templates/csharp/app-mvvm/.template.config/template.json).
Если у тебя уже установлен старый шаблон, сначала проверь `dotnet new
avalonia.mvvm --help` и его версии. Эти команды здесь приведены как инструкция;
сам учебный .NET-проект в этой задаче не создавался.

Практика: поменять заголовок, добавить кнопку и строку статуса. Разобраться,
зачем нужны Program.cs, App.axaml, MainWindow.axaml и `.axaml.cs`.
Сразу настроить `.editorconfig`, Nullable и analyzers для учебного проекта.
Готово: окно запускается на Mac; можешь объяснить путь от Program до View.

## 2. Освоить XAML и binding

Читать: [starter tutorial](https://docs.avaloniaui.net/docs/get-started/starter-tutorial),
[binding](https://docs.avaloniaui.net/docs/data-binding/introduction-to-data-binding).
Темы: Grid, StackPanel, размеры, отступы, Resources/Styles, DataContext,
OneWay/TwoWay, x:DataType и compiled bindings, ItemsControl и DataTemplate.
Текущий starter tutorial использует code-behind: сначала понять события,
затем перенести состояние и действия учебного окна в MVVM.

Практика: экран с тремя фиктивными профилями, выбором профиля и его описанием.
Готово: UI обновляется через binding без ручного присваивания текстов контролам.

## 3. Понять MVVM на одном сценарии

Читать: [ObservableObject](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/observableobject),
[ObservableProperty](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/generators/observableproperty),
[RelayCommand](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/generators/relaycommand).
Темы: PropertyChanged, ObservableCollection, partial/source generators,
ICommand/CanExecute, AsyncRelayCommand, CancellationToken и обработка ошибок.

Практика: FakeGameRunner имитирует подготовку; «Играть» меняет статус,
показывает прогресс и поддерживает отмену. Во время операции повторный запуск
недоступен. Ошибка отображается и возвращает экран в рабочее состояние.
Готово: две ViewModel-проверки на запрет повторного запуска и отмену/ошибку;
можешь объяснить цепочку View → команда → сценарий → property notification.

## 4. Разобраться с UI-потоком и отделить ядро

Читать: [threading model](https://docs.avaloniaui.net/docs/app-development/threading),
[DI](https://docs.avaloniaui.net/docs/app-development/dependency-injection).
Нельзя блокировать UI через `.Result`, `.Wait()` или тяжёлую обработку VPK.
`async` сам по себе не переносит вычисления на фоновый поток. Для CPU-нагрузки
выбирать фоновое выполнение; I/O ожидать асинхронно. Обновления bound-коллекций
и состояния экрана доставлять в UI-поток. `Progress<T>`, созданный на UI-потоке,
захватывает его контекст; фоновые callbacks при необходимости маршалить через
`Dispatcher.UIThread`.

Практика: выделить три проекта, внедрить LaunchGameUseCase и FakeGameRunner,
сохранить один профиль в JSON. Подписки и фоновые задачи имеют владельца,
отменяются/освобождаются при закрытии экрана или приложения.
В Core разделить Domain/Application; сверить ProjectReference с правилом
зависимостей, настроить общие `.editorconfig` и `Directory.Build.props`.
Готово: Core можно вызвать из CLI; он не импортирует Avalonia или Toolkit.

## 5. Подключить настоящий запуск

Практика: заменить FakeGameRunner на WineGameRunner. Использовать
`ProcessStartInfo.ArgumentList` и `Environment`, перенаправлять/читать логи,
выдавать понятные ошибки и таймаут ожидания процесса игры. Повторно использовать
Steam только своего prefix. Отмена ожидания не должна неявно убивать игру;
остановка игры — отдельная команда с определённым владением процессами.

Готово: запуск через UI воспроизводит проверенный CLI-маршрут, UI остаётся
отзывчивым, закрытие окна обрабатывает задачи/процессы по явной политике.

## 6. Добавить моды с восстановлением

Практика: сначала локальный импорт VPK и один профиль; затем Vanilla/Modded.
Рассчитывать InstallPlan до записи: конфликты виртуальных путей, порядок,
варианты и контрольные суммы. Применение: staging → journal → замена файлов.
После патча Steam читать актуальную конфигурацию, а не возвращать старую копию.
Не трогать неизвестные ручные моды без включения их в план.

Готово: проверки в временных каталогах покрывают конфликт, отмену, сбой между
заменами, восстановление после перезапуска и сохранение чужих файлов. Журнал
нужен для восстановления после завершения процесса; сообщения в памяти и
брокер не делают многофайловую операцию атомарной.

## 7. Собрать приложение для другого Mac

Читать: [macOS deployment](https://docs.avaloniaui.net/docs/deployment/macos).
Практика: self-contained osx-arm64, `.app`, каталог Application Support,
подписание/notarization для публичного выпуска, GitHub Releases и инструкция
сборки. Native AOT и обновлятор добавлять после работающего обычного выпуска.
Условия распространения каждого внешнего runtime проверять отдельно.

Готово: на другом Mac без установленного .NET работает установка/импорт среды,
запуск, профиль и восстановление; логи позволяют разобрать неудачный запуск.

## Сообщения: что действительно нужно

RabbitMQ/Kafka в основной маршрут не включаем. Оба технически доступны из .NET
через [RabbitMQ.Client](https://www.rabbitmq.com/client-libraries/dotnet-api-guide)
и [Confluent.Kafka](https://docs.confluent.io/kafka-clients/dotnet/current/overview.html),
но требуют работающего внешнего брокера. Для одной локальной программы такой
транспорт не решает текущую задачу.

- Действие с результатом/ошибкой: прямой вызов сервиса и `await`.
- Прогресс текущей операции: `IProgress<T>`.
- Очередь фоновых загрузок при её появлении: bounded
  [Channel<T>](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels).
  Это очередь в памяти, не журнал и не broadcast всем читателям.
- Необязательные уведомления между экранами: обычные события или
  [WeakReferenceMessenger](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/messenger).
  Messenger не заменяет явную последовательность критичных операций.

Удалённый каталог в будущем можно подключить HTTP-клиентом; если серверу
понадобится брокер, это отдельное решение для серверной части.

## Первая законченная учебная задача

Один экран: список профилей, «Играть», «Отмена», статус и ProgressBar.
Фиктивная подготовка длится несколько секунд. Двойное нажатие не запускает
вторую операцию, отмена работает, ошибка видна, после неё можно повторить.
Сначала добейся этого поведения и объясни binding/команду/уведомления. Затем
подключай файловые операции и настоящий runtime.

UML в `architecture.puml` содержит компоненты, классы и успешный сценарий
запуска. Подписи методов — проект контрактов, а не обещание готовой реализации.
