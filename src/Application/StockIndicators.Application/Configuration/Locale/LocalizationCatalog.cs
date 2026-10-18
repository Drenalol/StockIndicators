using System.Globalization;

namespace StockIndicators.Application.Configuration.Locale;

/// <summary>
/// The single store of localized UI strings, keyed by <see cref="LocalizationKey"/> and
/// language code. Lookup falls back to <see cref="UserLocale.DefaultLanguage"/> when a
/// language has no value for a key, so a partially translated language still works and a
/// missing key never throws at runtime (a test guards full coverage instead).
///
/// Adding a language: one code constant and registry entry in <see cref="UserLocale"/>,
/// then a column in every row below. Adding a string: one enum member and one row.
/// Existing code and attribute usages stay untouched either way.
/// </summary>
public static class LocalizationCatalog
{
  private const string En = UserLocale.English;
  private const string Ru = UserLocale.Russian;
  private const string Es = UserLocale.Spanish;
  private const string It = UserLocale.Italian;
  private const string De = UserLocale.German;
  private const string Fr = UserLocale.French;
  private const string PtBr = UserLocale.PortugueseBrazil;
  private const string Uk = UserLocale.Ukrainian;
  private const string Be = UserLocale.Belarusian;

  private static readonly IReadOnlyDictionary<LocalizationKey, IReadOnlyDictionary<string, string>> Entries =
    new Dictionary<LocalizationKey, IReadOnlyDictionary<string, string>>
    {
      [LocalizationKey.Back] = Row((Ru, "Назад"), (En, "Back"), (Es, "Atrás"), (It, "Indietro"), (De, "Zurück"), (Fr, "Retour"), (PtBr, "Voltar"), (Uk, "Назад"), (Be, "Назад")),
      [LocalizationKey.Subscriptions] = Row((Ru, "Подписки"), (En, "Subscriptions"), (Es, "Suscripciones"), (It, "Abbonamenti"), (De, "Abonnements"), (Fr, "Abonnements"), (PtBr, "Assinaturas"), (Uk, "Підписки"), (Be, "Падпіскі")),
      [LocalizationKey.Indicators] = Row((Ru, "Индикаторы"), (En, "Indicators"), (Es, "Indicadores"), (It, "Indicatori"), (De, "Indikatoren"), (Fr, "Indicateurs"), (PtBr, "Indicadores"), (Uk, "Індикатори"), (Be, "Індыкатары")),
      [LocalizationKey.Instruments] = Row((Ru, "Инструменты"), (En, "Instruments"), (Es, "Instrumentos"), (It, "Strumenti"), (De, "Instrumente"), (Fr, "Instruments"), (PtBr, "Instrumentos"), (Uk, "Інструменти"), (Be, "Інструменты")),
      [LocalizationKey.Settings] = Row((Ru, "Настройки"), (En, "Settings"), (Es, "Ajustes"), (It, "Impostazioni"), (De, "Einstellungen"), (Fr, "Paramètres"), (PtBr, "Configurações"), (Uk, "Налаштування"), (Be, "Налады")),
      [LocalizationKey.LinkThisChat] = Row((Ru, "Привязать этот чат"), (En, "Link this chat"), (Es, "Vincular este chat"), (It, "Collega questa chat"), (De, "Diesen Chat verknüpfen"), (Fr, "Lier cette discussion"), (PtBr, "Vincular este chat"), (Uk, "Прив'язати цей чат"), (Be, "Прывязаць гэты чат")),
      [LocalizationKey.ManageChats] = Row((Ru, "Управлять чатами"), (En, "Manage chats"), (Es, "Gestionar chats"), (It, "Gestisci le chat"), (De, "Chats verwalten"), (Fr, "Gérer les discussions"), (PtBr, "Gerenciar chats"), (Uk, "Керувати чатами"), (Be, "Кіраваць чатамі")),
      [LocalizationKey.AddSubscription] = Row((Ru, "Добавить подписку"), (En, "Add Subscription"), (Es, "Añadir suscripción"), (It, "Aggiungi abbonamento"), (De, "Abonnement hinzufügen"), (Fr, "Ajouter un abonnement"), (PtBr, "Adicionar assinatura"), (Uk, "Додати підписку"), (Be, "Дадаць падпіску")),
      [LocalizationKey.CreateSubscription] = Row((Ru, "Создать подписку"), (En, "Create subscription"), (Es, "Crear suscripción"), (It, "Crea abbonamento"), (De, "Abonnement erstellen"), (Fr, "Créer un abonnement"), (PtBr, "Criar assinatura"), (Uk, "Створити підписку"), (Be, "Стварыць падпіску")),
      [LocalizationKey.EditSubscription] = Row((Ru, "Изменить подписку"), (En, "Edit subscription"), (Es, "Editar suscripción"), (It, "Modifica abbonamento"), (De, "Abonnement bearbeiten"), (Fr, "Modifier l'abonnement"), (PtBr, "Editar assinatura"), (Uk, "Редагувати підписку"), (Be, "Рэдагаваць падпіску")),
      [LocalizationKey.Subscribe] = Row((Ru, "Подписаться"), (En, "Subscribe"), (Es, "Suscribirse"), (It, "Abbonati"), (De, "Abonnieren"), (Fr, "S'abonner"), (PtBr, "Assinar"), (Uk, "Підписатися"), (Be, "Падпісацца")),
      [LocalizationKey.PrevPage] = Row((Ru, "◀ Назад"), (En, "◀ Prev"), (Es, "◀ Anterior"), (It, "◀ Indietro"), (De, "◀ Zurück"), (Fr, "◀ Précédent"), (PtBr, "◀ Anterior"), (Uk, "◀ Назад"), (Be, "◀ Назад")),
      [LocalizationKey.NextPage] = Row((Ru, "Вперёд ▶"), (En, "Next ▶"), (Es, "Siguiente ▶"), (It, "Avanti ▶"), (De, "Weiter ▶"), (Fr, "Suivant ▶"), (PtBr, "Próximo ▶"), (Uk, "Далі ▶"), (Be, "Далей ▶")),
      [LocalizationKey.IndicatorsListTitle] = Row((Ru, "Список индикаторов"), (En, "Indicator list"), (Es, "Lista de indicadores"), (It, "Elenco indicatori"), (De, "Indikatorenliste"), (Fr, "Liste des indicateurs"), (PtBr, "Lista de indicadores"), (Uk, "Список індыкатараў"), (Be, "Спіс індыкатараў")),
      [LocalizationKey.InstrumentsListTitle] = Row((Ru, "Список инструментов{pages}"), (En, "Instrument list{pages}"), (Es, "Lista de instrumentos{pages}"), (It, "Elenco strumenti{pages}"), (De, "Instrumentenliste{pages}"), (Fr, "Liste des instruments{pages}"), (PtBr, "Lista de instrumentos{pages}"), (Uk, "Список інструментаў{pages}"), (Be, "Спіс інструментаў{pages}")),
      [LocalizationKey.InstrumentsSearchResultsTitle] = Row((Ru, "Результаты поиска «{search}»{pages}"), (En, "Search results for \"{search}\"{pages}"), (Es, "Resultados de búsqueda de «{search}»{pages}"), (It, "Risultati della ricerca «{search}»{pages}"), (De, "Suchergebnisse für \"{search}\"{pages}"), (Fr, "Résultats de recherche pour \"{search}\"{pages}"), (PtBr, "Resultados da pesquisa de \"{search}\"{pages}"), (Uk, "Результати пошуку «{search}»{pages}"), (Be, "Вынікі пошуку «{search}»{pages}")),
      [LocalizationKey.NoInstruments] = Row((Ru, "Ничего не найдено"), (En, "Nothing found"), (Es, "No se ha encontrado nada"), (It, "Nessun risultato"), (De, "Nichts gefunden"), (Fr, "Aucun résultat"), (PtBr, "Nada encontrado"), (Uk, "Нічого не знайдено"), (Be, "Нічога не знойдзена")),
      [LocalizationKey.InstrumentsSearchHint] = Row((Ru, "\n\nНапишите тикер или название, чтобы искать."), (En, "\n\nType a ticker or name to search."), (Es, "\n\nEscribe un ticker o un nombre para buscar."), (It, "\n\nDigita un ticker o un nome per cercare."), (De, "\n\nGib einen Ticker oder Namen ein, um zu suchen."), (Fr, "\n\nSaisissez un ticker ou un nom pour rechercher."), (PtBr, "\n\nDigite um ticker ou um nome para pesquisar."), (Uk, "\n\nНапишіть тікер або назву, щоб шукати."), (Be, "\n\nНапішыце тыкер або назву, каб шукаць.")),
      [LocalizationKey.IndicatorParametersHeader] = Row((Ru, "\n\nПараметры:"), (En, "\n\nParameters:"), (Es, "\n\nParámetros:"), (It, "\n\nParametri:"), (De, "\n\nParameter:"), (Fr, "\n\nParamètres :"), (PtBr, "\n\nParâmetros:"), (Uk, "\n\nПараметри:"), (Be, "\n\nПараметры:")),
      [LocalizationKey.IndicatorNoParameters] = Row((Ru, "нет параметров"), (En, "no parameters"), (Es, "sin parámetros"), (It, "nessun parametro"), (De, "keine Parameter"), (Fr, "aucun paramètre"), (PtBr, "sem parâmetros"), (Uk, "немає параметрів"), (Be, "няма параметраў")),
      [LocalizationKey.InstrumentFieldName] = Row((Ru, "Название"), (En, "Name"), (Es, "Nombre"), (It, "Nome"), (De, "Name"), (Fr, "Nom"), (PtBr, "Nome"), (Uk, "Назва"), (Be, "Назва")),
      [LocalizationKey.InstrumentFieldType] = Row((Ru, "Тип"), (En, "Type"), (Es, "Tipo"), (It, "Tipo"), (De, "Typ"), (Fr, "Type"), (PtBr, "Tipo"), (Uk, "Тип"), (Be, "Тып")),
      [LocalizationKey.InstrumentFieldCurrency] = Row((Ru, "Валюта"), (En, "Currency"), (Es, "Moneda"), (It, "Valuta"), (De, "Währung"), (Fr, "Devise"), (PtBr, "Moeda"), (Uk, "Валюта"), (Be, "Валюта")),
      [LocalizationKey.InstrumentFieldSector] = Row((Ru, "Сектор"), (En, "Sector"), (Es, "Sector"), (It, "Settore"), (De, "Sektor"), (Fr, "Secteur"), (PtBr, "Setor"), (Uk, "Сектор"), (Be, "Сектар")),
      [LocalizationKey.InstrumentFieldIndustry] = Row((Ru, "Отрасль"), (En, "Industry"), (Es, "Industria"), (It, "Industria"), (De, "Branche"), (Fr, "Secteur d'activité"), (PtBr, "Indústria"), (Uk, "Галузь"), (Be, "Галіна")),
      [LocalizationKey.InstrumentFieldCountry] = Row((Ru, "Страна"), (En, "Country"), (Es, "País"), (It, "Paese"), (De, "Land"), (Fr, "Pays"), (PtBr, "País"), (Uk, "Країна"), (Be, "Краіна")),
      [LocalizationKey.SubscriptionFieldInstrument] = Row((Ru, "Инструмент"), (En, "Instrument"), (Es, "Instrumento"), (It, "Strumento"), (De, "Instrument"), (Fr, "Instrument"), (PtBr, "Instrumento"), (Uk, "Інструмент"), (Be, "Інструмент")),
      [LocalizationKey.SubscriptionFieldIndicator] = Row((Ru, "Индикатор"), (En, "Indicator"), (Es, "Indicador"), (It, "Indicatore"), (De, "Indikator"), (Fr, "Indicateur"), (PtBr, "Indicador"), (Uk, "Індикатор"), (Be, "Індыкатар")),
      [LocalizationKey.SubscriptionFieldCooldown] = Row((Ru, "Пауза"), (En, "Cooldown"), (Es, "Enfriamiento"), (It, "Pausa"), (De, "Mindestabstand"), (Fr, "Temporisation"), (PtBr, "Intervalo"), (Uk, "Пауза"), (Be, "Паўза")),
      [LocalizationKey.SubscriptionFieldStatus] = Row((Ru, "Статус"), (En, "Status"), (Es, "Estado"), (It, "Stato"), (De, "Status"), (Fr, "Statut"), (PtBr, "Estado"), (Uk, "Статус"), (Be, "Статус")),
      [LocalizationKey.SubscriptionActive] = Row((Ru, "активна"), (En, "active"), (Es, "activa"), (It, "attivo"), (De, "aktiv"), (Fr, "actif"), (PtBr, "ativa"), (Uk, "активна"), (Be, "актыўная")),
      [LocalizationKey.SubscriptionInactive] = Row((Ru, "выключена"), (En, "inactive"), (Es, "inactiva"), (It, "inattivo"), (De, "inaktiv"), (Fr, "inactif"), (PtBr, "inativa"), (Uk, "вимкнено"), (Be, "выключаная")),
      [LocalizationKey.SubscriptionPaused] = Row((Ru, "на паузе (нужен премиум)"), (En, "paused (premium required)"), (Es, "en pausa (requiere premium)"), (It, "in pausa (serve il premium)"), (De, "pausiert (Premium erforderlich)"), (Fr, "en pause (premium requis)"), (PtBr, "pausada (requer premium)"), (Uk, "на паузі (потрібен преміум)"), (Be, "на паўзе (патрэбны прэміум)")),
      [LocalizationKey.SubscriptionFieldParameters] = Row((Ru, "Параметры"), (En, "Parameters"), (Es, "Parámetros"), (It, "Parametri"), (De, "Parameter"), (Fr, "Paramètres"), (PtBr, "Parâmetros"), (Uk, "Параметри"), (Be, "Параметры")),
      [LocalizationKey.SubscriptionFieldChannels] = Row((Ru, "Каналы"), (En, "Channels"), (Es, "Canales"), (It, "Canali"), (De, "Kanäle"), (Fr, "Canaux"), (PtBr, "Canais"), (Uk, "Канали"), (Be, "Каналы")),
      [LocalizationKey.StartNotLinked] = Row(
        (Ru, "Привет, {name}!\n\n" +
        "<b>Stock Indicators</b> - мониторинг торговых сигналов: алерт в этот чат, когда индикатор на выбранном инструменте срабатывает.\n\n" +
        "Этот чат ещё не привязан к экземпляру. Откройте страницу настроек своего экземпляра и создайте одноразовый токен привязки - затем нажмите «{link}» и Start или отправьте сюда сообщение /link &lt;токен&gt;.\n\n" +
        "Сменить язык бота: /language"),
        (En, "Hello, {name}!\n\n" +
        "<b>Stock Indicators</b> - trading signal monitoring: an alert to this chat the moment an indicator on a chosen instrument triggers.\n\n" +
        "This chat is not linked to an instance yet. Open your instance's settings page and create a one-time link token - then press \"{link}\" and Start, or send the message /link &lt;token&gt; here.\n\n" +
        "To change my language: /language"),
        (Es, "¡Hola, {name}!\n\n" +
        "<b>Stock Indicators</b> - monitorización de señales de trading: una alerta a este chat en el momento en que se activa un indicador del instrumento elegido.\n\n" +
        "Este chat aún no está vinculado a ninguna instancia. Abre la página de ajustes de tu instancia y crea un token de vinculación de un solo uso: luego pulsa «{link}» y Start, o envía aquí el mensaje /link &lt;token&gt;.\n\n" +
        "Para cambiar mi idioma: /language"),
        (It, "Ciao, {name}!\n\n" +
        "<b>Stock Indicators</b> - monitoraggio dei segnali di trading: un avviso in questa chat quando un indicatore sullo strumento scelto scatta.\n\n" +
        "Questa chat non è ancora collegata a un'istanza. Apri la pagina delle impostazioni della tua istanza e crea un token di collegamento monouso: poi premi \"{link}\" e Start, oppure invia qui il messaggio /link &lt;token&gt;.\n\n" +
        "Per cambiare la mia lingua: /language"),
        (De, "Hallo, {name}!\n\n" +
        "<b>Stock Indicators</b> - Überwachung von Handelssignalen: eine Alert in diesen Chat, sobald ein Indikator am gewählten Instrument auslöst.\n\n" +
        "Dieser Chat ist noch mit keiner Instanz verknüpft. Öffne die Seite deiner Instanz und erstelle einen einmaligen Verknüpfungs-Token - dann \"{link}\" und Start tippen oder die Nachricht /link &lt;token&gt; hier senden.\n\n" +
        "Um meine Sprache zu wechseln: /language"),
        (Fr, "Bonjour, {name}!\n\n" +
        "<b>Stock Indicators</b> - surveillance des signaux de trading : une alerte dans cette discussion dès qu'un indicateur se déclenche sur l'instrument choisi.\n\n" +
        "Cette discussion n'est encore liée à aucune instance. Ouvrez la page des paramètres de votre instance et créez un jeton de liaison à usage unique - puis appuyez sur « {link} » et Start, ou envoyez ici le message /link &lt;jeton&gt;.\n\n" +
        "Pour changer ma langue : /language"),
        (PtBr, "Olá, {name}!\n\n" +
        "<b>Stock Indicators</b> - monitoramento de sinais de trading: um alerta neste chat no momento em que um indicador do instrumento escolhido é acionado.\n\n" +
        "Este chat ainda não está vinculado a uma instância. Abra a página de configurações da sua instância e crie um token de vinculação de uso único - depois pressione \"{link}\" e Start, ou envie aqui a mensagem /link &lt;token&gt;.\n\n" +
        "Para mudar o meu idioma: /language"),
        (Uk, "Привіт, {name}!\n\n" +
        "<b>Stock Indicators</b> - моніторинг торгових сигналів: алерт у цей чат, коли індикатор на вибраному інструменті спрацьовує.\n\n" +
        "Цей чат ще не прив'язаний до екземпляра. Відкрийте сторінку налаштувань свого екземпляра та створіть одноразовий токен прив'язки - потім натисніть «{link}» і Start або надішліть сюди повідомлення /link &lt;токен&gt;.\n\n" +
        "Змінити мову бота: /language"),
        (Be, "Вітаю, {name}!\n\n" +
        "<b>Stock Indicators</b> - маніторынг гандлёвых сігналаў: апавяшчэнне ў гэты чат, калі індыкатар на абраным інструменце спрацоўвае.\n\n" +
        "Гэты чат яшчэ не прывязаны да экзэмпляра. Адкрыйце старонку налад свайго экзэмпляра і стварыце аднаразовы токен прывязкі - затым націсніце «{link}» і Start або адпраўце сюды паведамленне /link &lt;токен&gt;.\n\n" +
        "Змяніць мову бота: /language")),
      [LocalizationKey.SettingsUnlinked] = Row(
        (Ru, "Этот чат больше не привязан к экземпляру.\n\nНажмите «{link}», откройте страницу настроек и создайте одноразовый токен привязки - затем отправьте его сюда сообщением /link &lt;токен&gt;."),
        (En, "This chat is no longer linked to the instance.\n\nPress \"{link}\", open the settings page and create a one-time link token - then send it here as the message /link &lt;token&gt;."),
        (Es, "Este chat ya no está vinculado a la instancia.\n\nPulsa «{link}», abre la página de ajustes y crea un token de vinculación de un solo uso - luego envíalo aquí con el mensaje /link &lt;token&gt;."),
        (It, "Questa chat non è più collegata all'istanza.\n\nPremi \"{link}\", apri la pagina delle impostazioni e crea un token di collegamento monouso - poi invialo qui con il messaggio /link &lt;token&gt;."),
        (De, "Dieser Chat ist nicht mehr mit der Instanz verknüpft.\n\nDrücke \"{link}\", öffne die Seite der Einstellungen und erstelle einen einmaligen Verknüpfungs-Token - sende ihn dann hier als Nachricht /link &lt;token&gt;."),
        (Fr, "Cette discussion n'est plus liée à l'instance.\n\nAppuyez sur « {link} », ouvrez la page des paramètres et créez un jeton de liaison à usage unique - puis envoyez-le ici avec le message /link &lt;jeton&gt;."),
        (PtBr, "Este chat não está mais vinculado à instância.\n\nPressione \"{link}\", abra a página de configurações e crie um token de vinculação de uso único - depois envie-o aqui com a mensagem /link &lt;token&gt;."),
        (Uk, "Цей чат більше не прив'язаний до екземпляра.\n\nНатисніть «{link}», відкрийте сторінку налаштувань і створіть одноразовий токен прив'язки - потім надішліть його сюди повідомленням /link &lt;токен&gt;."),
        (Be, "Гэты чат больш не прывязаны да экзэмпляра.\n\nНацісніце «{link}», адкрыйце старонку налад і стварыце аднаразовы токен прывязкі - затым адпраўце яго сюды паведамленнем /link &lt;токен&gt;.")),
      [LocalizationKey.StartAbout] = Row(
        (Ru, "Привет, {name}!\n\n" +
        "<b>Stock Indicators</b> - мониторинг торговых сигналов.\n\n" +
        "Мы следим за индикаторами на выбранных инструментах (RSI, MACD, Bollinger Bands, скользящие средние …) " +
        "и присылаем алерт сюда, в Telegram, когда заданное условие срабатывает.\n\n" +
        "Как пользоваться:\n" +
        "1. «{subscriptions}» → «{add}» - откроется веб-приложение\n" +
        "2. Там выбираете инструмент, индикатор и условие срабатывания\n" +
        "3. Готово - алерты приходят в этот чат\n\n" +
        "«{indicators}» - справочник: что считает каждый индикатор и его параметры\n" +
        "«{instruments}» - поиск инструментов по тикеру или названию\n" +
        "«{settings}» - язык, часовой пояс и привязка чатов"),
        (En, "Hello, {name}!\n\n" +
        "<b>Stock Indicators</b> - trading signal monitoring.\n\n" +
        "We watch indicators on the instruments you pick (RSI, MACD, Bollinger Bands, moving averages …) " +
        "and send an alert here in Telegram the moment a condition triggers.\n\n" +
        "How to use:\n" +
        "1. \"{subscriptions}\" → \"{add}\" opens the mini app\n" +
        "2. There you choose an instrument, indicator and trigger condition\n" +
        "3. Done - alerts arrive in this chat\n\n" +
        "\"{indicators}\" - a guide: what each indicator computes and its parameters\n" +
        "\"{instruments}\" - search instruments by ticker or name\n" +
        "\"{settings}\" - language, time zone and chat linking"),
        (Es, "¡Hola, {name}!\n\n" +
        "<b>Stock Indicators</b> - monitorización de señales de trading.\n\n" +
        "Vigilamos los indicadores de los instrumentos que elijas (RSI, MACD, Bollinger Bands, medias móviles …) " +
        "y enviamos una alerta aquí, a Telegram, cuando se cumple la condición configurada.\n\n" +
        "Cómo usarlo:\n" +
        "1. «{subscriptions}» → «{add}» - se abrirá la aplicación web\n" +
        "2. Allí eliges el instrumento, el indicador y la condición de activación\n" +
        "3. Listo - las alertas llegan a este chat\n\n" +
        "«{indicators}» - guía: qué calcula cada indicador y sus parámetros\n" +
        "«{instruments}» - busca instrumentos por ticker o nombre\n" +
        "«{settings}» - idioma, zona horaria y vinculación de chats"),
        (It, "Ciao, {name}!\n\n" +
        "<b>Stock Indicators</b> - monitoraggio dei segnali di trading.\n\n" +
        "Teniamo d'occhio gli indicatori sugli strumenti che scegli (RSI, MACD, Bollinger Bands, medie mobili …) " +
        "e inviamo un avviso qui su Telegram quando la condizione configurata scatta.\n\n" +
        "Come si usa:\n" +
        "1. «{subscriptions}» → «{add}» - si apre la web app\n" +
        "2. Lì scegli lo strumento, l'indicatore e la condizione di attivazione\n" +
        "3. Fatto - gli avvisi arrivano in questa chat\n\n" +
        "«{indicators}» - guida: cosa calcola ogni indicatore e i suoi parametri\n" +
        "«{instruments}» - cerca strumenti per ticker o nome\n" +
        "«{settings}» - lingua, fuso orario e collegamento delle chat"),
        (De, "Hallo, {name}!\n\n" +
        "<b>Stock Indicators</b> - Überwachung von Handelssignalen.\n\n" +
        "Wir beobachten Indikatoren auf den von dir gewählten Instrumenten (RSI, MACD, Bollinger Bänder, gleitende Durchschnitte …) " +
        "und senden sofort hierher in Telegram eine Alert, wenn die eingestellte Bedingung eintritt.\n\n" +
        "So geht's:\n" +
        "1. \"{subscriptions}\" → \"{add}\" - die Web-App öffnet sich\n" +
        "2. Dort wählst du Instrument, Indikator und Auslösebedingung\n" +
        "3. Fertig - Alerts kommen in diesen Chat\n\n" +
        "\"{indicators}\" - Nachschlagewerk: was jeder Indikator berechnet und seine Parameter\n" +
        "\"{instruments}\" - Instrumente nach Ticker oder Namen suchen\n" +
        "\"{settings}\" - Sprache, Zeitzone und Chat-Verknüpfung"),
        (Fr, "Bonjour, {name}!\n\n" +
        "<b>Stock Indicators</b> - surveillance des signaux de trading.\n\n" +
        "Nous surveillons les indicateurs sur les instruments que vous choisissez (RSI, MACD, Bandes de Bollinger, moyennes mobiles …) " +
        "et envoyons une alerte ici sur Telegram dès que la condition configurée se déclenche.\n\n" +
        "Comment ça marche :\n" +
        "1. « {subscriptions} » → « {add} » - l'application web s'ouvre\n" +
        "2. Vous y choisissez l'instrument, l'indicateur et la condition de déclenchement\n" +
        "3. Terminé - les alertes arrivent dans cette discussion\n\n" +
        "« {indicators} » - le guide : ce que calcule chaque indicateur et ses paramètres\n" +
        "« {instruments} » - recherche d'instruments par ticker ou nom\n" +
        "« {settings} » - langue, fuseau horaire et liaison des discussions"),
        (PtBr, "Olá, {name}!\n\n" +
        "<b>Stock Indicators</b> - monitoramento de sinais de trading.\n\n" +
        "Acompanhamos os indicadores nos instrumentos que você escolher (RSI, MACD, Bandas de Bollinger, médias móveis …) " +
        "e enviamos um alerta aqui no Telegram quando a condição configurada é atingida.\n\n" +
        "Como usar:\n" +
        "1. \"{subscriptions}\" → \"{add}\" - o aplicativo web será aberto\n" +
        "2. Lá você escolhe o instrumento, o indicador e a condição de acionamento\n" +
        "3. Pronto - os alertas chegam neste chat\n\n" +
        "\"{indicators}\" - guia: o que cada indicador calcula e seus parâmetros\n" +
        "\"{instruments}\" - busca de instrumentos por ticker ou nome\n" +
        "\"{settings}\" - idioma, fuso horário e vinculação de chats"),
        (Uk, "Привіт, {name}!\n\n" +
        "<b>Stock Indicators</b> - моніторинг торгових сигналів.\n\n" +
        "Ми стежимо за індикаторами на вибраних інструментах (RSI, MACD, Bollinger Bands, ковзні середні …) " +
        "і надсилаємо алерт сюди, в Telegram, коли задана умова спрацьовує.\n\n" +
        "Як користуватися:\n" +
        "1. «{subscriptions}» → «{add}» - відкриється веб-застосунок\n" +
        "2. Там вибираєте інструмент, індикатор і умову спрацювання\n" +
        "3. Готово - алерти надходять у цей чат\n\n" +
        "«{indicators}» - довідник: що рахує кожен індикатор і його параметри\n" +
        "«{instruments}» - пошук інструментів за тікером або назвою\n" +
        "«{settings}» - мова, часовий пояс і прив'язка чатів"),
        (Be, "Вітаю, {name}!\n\n" +
        "<b>Stock Indicators</b> - маніторынг гандлёвых сігналаў.\n\n" +
        "Мы сочым за індыкатарамі на абраных інструментах (RSI, MACD, Bollinger Bands, слізгальныя сярэднія …) " +
        "і дасылаем апавяшчэнне сюды, у Telegram, калі зададзеная ўмова спрацоўвае.\n\n" +
        "Як карыстацца:\n" +
        "1. «{subscriptions}» → «{add}» - адкрыецца вэб-дадатак\n" +
        "2. Там выбіраеце інструмент, індыкатар і ўмову спрацоўвання\n" +
        "3. Гатова - апавяшчэнні прыходзяць у гэты чат\n\n" +
        "«{indicators}» - даведнік: што лічыць кожны індыкатар і яго параметры\n" +
        "«{instruments}» - пошук інструментаў па тыкеры або назве\n" +
        "«{settings}» - мова, часаўы пояс і прывязка чатаў")),
      [LocalizationKey.SettingsTitle] = Row((Ru, "Настройки"), (En, "Settings"), (Es, "Ajustes"), (It, "Impostazioni"), (De, "Einstellungen"), (Fr, "Paramètres"), (PtBr, "Configurações"), (Uk, "Налаштування"), (Be, "Налады")),
      [LocalizationKey.SettingsLanguageLabel] = Row((Ru, "Язык"), (En, "Language"), (Es, "Idioma"), (It, "Lingua"), (De, "Sprache"), (Fr, "Langue"), (PtBr, "Idioma"), (Uk, "Мова"), (Be, "Мова")),
      [LocalizationKey.LanguageNameRu] = Row((Ru, "Русский"), (En, "Russian"), (Es, "Ruso"), (It, "Russo"), (De, "Russisch"), (Fr, "Russe"), (PtBr, "Russo"), (Uk, "Російська"), (Be, "Руская")),
      [LocalizationKey.LanguageNameEn] = Row((Ru, "Английский"), (En, "English"), (Es, "Inglés"), (It, "Inglese"), (De, "Englisch"), (Fr, "Anglais"), (PtBr, "Inglês"), (Uk, "Англійська"), (Be, "Ангельская")),
      [LocalizationKey.LanguageNameEs] = Row((Ru, "Испанский"), (En, "Spanish"), (Es, "Español"), (It, "Spagnolo"), (De, "Spanisch"), (Fr, "Espagnol"), (PtBr, "Espanhol"), (Uk, "Іспанська"), (Be, "Іспанская")),
      [LocalizationKey.LanguageNameIt] = Row((Ru, "Итальянский"), (En, "Italian"), (Es, "Italiano"), (It, "Italiano"), (De, "Italienisch"), (Fr, "Italien"), (PtBr, "Italiano"), (Uk, "Італійська"), (Be, "Італьянская")),
      [LocalizationKey.LanguageNameDe] = Row((Ru, "Немецкий"), (En, "German"), (Es, "Alemán"), (It, "Tedesco"), (De, "Deutsch"), (Fr, "Allemand"), (PtBr, "Alemão"), (Uk, "Німецька"), (Be, "Нямецкая")),
      [LocalizationKey.LanguageNameFr] = Row((Ru, "Французский"), (En, "French"), (Es, "Francés"), (It, "Francese"), (De, "Französisch"), (Fr, "Français"), (PtBr, "Francês"), (Uk, "Французька"), (Be, "Французская")),
      [LocalizationKey.LanguageNamePtBr] = Row((Ru, "Португальский (Бразилия)"), (En, "Portuguese (Brazil)"), (Es, "Portugués (Brasil)"), (It, "Portoghese (Brasile)"), (De, "Portugiesisch (Brasilien)"), (Fr, "Portugais (Brésil)"), (PtBr, "Português (Brasil)"), (Uk, "Португальська (Бразилія)"), (Be, "Партугальская (Бразылія)")),
      [LocalizationKey.LanguageNameUk] = Row((Ru, "Украинский"), (En, "Ukrainian"), (Es, "Ucraniano"), (It, "Ucraino"), (De, "Ukrainisch"), (Fr, "Ukrainien"), (PtBr, "Ucraniano"), (Uk, "Українська"), (Be, "Украінская")),
      [LocalizationKey.LanguageNameBe] = Row((Ru, "Белорусский"), (En, "Belarusian"), (Es, "Bielorruso"), (It, "Bielorusso"), (De, "Belarussisch"), (Fr, "Biélorusse"), (PtBr, "Bielorrusso"), (Uk, "Білоруська"), (Be, "Беларуская")),
            [LocalizationKey.No] = Row((Ru, "нет"), (En, "no"), (Es, "no"), (It, "no"), (De, "nein"), (Fr, "non"), (PtBr, "não"), (Uk, "ні"), (Be, "не")),
      [LocalizationKey.LanguageListTitle] = Row((Ru, "Язык интерфейса бота"), (En, "Bot interface language"), (Es, "Idioma de la interfaz del bot"), (It, "Lingua dell'interfaccia del bot"), (De, "Sprache der Bot-Oberfläche"), (Fr, "Langue de l'interface du bot"), (PtBr, "Idioma da interface do bot"), (Uk, "Мова інтерфейсу бота"), (Be, "Мова інтэрфейсу бота")),
      [LocalizationKey.TimeZoneListTitle] = Row((Ru, "Часовой пояс"), (En, "Time zone"), (Es, "Zona horaria"), (It, "Fuso orario"), (De, "Zeitzone"), (Fr, "Fuseau horaire"), (PtBr, "Fuso horário"), (Uk, "Часовий пояс"), (Be, "Часавы пояс")),
      [LocalizationKey.TimeZoneListHint] = Row((Ru, "\n\nМожно ввести свой IANA id, например America/New_York."), (En, "\n\nYou can also type any IANA id, e.g. America/New_York."), (Es, "\n\nTambién puedes escribir cualquier id IANA, p. ej. America/New_York."), (It, "\n\nPuoi anche digitare qualsiasi id IANA, es. America/New_York."), (De, "\n\nDu kannst auch jede IANA-ID eingeben, z. B. America/New_York."), (Fr, "\n\nVous pouvez aussi saisir n'importe quel id IANA, p. ex. America/New_York."), (PtBr, "\n\nVocê também pode digitar qualquer id IANA, ex.: America/New_York."), (Uk, "\n\nМожна ввести будь-який IANA id, напр. America/New_York."), (Be, "\n\nМожна ўвесці любы IANA id, напр. America/New_York.")),
      [LocalizationKey.TimeZoneInvalid] = Row((Ru, "Некорректный IANA id часового пояса: {value}"), (En, "Invalid IANA time zone id: {value}"), (Es, "IANA id de zona horaria no válido: {value}"), (It, "Id IANA del fuso orario non valido: {value}"), (De, "Ungültige IANA-Zeitzone-ID: {value}"), (Fr, "Id IANA de fuseau horaire non valide : {value}"), (PtBr, "Id IANA de fuso horário inválido: {value}"), (Uk, "Некоректний IANA id часового поясу: {value}"), (Be, "Некарэктны IANA id часавага поясу: {value}")),
                                                                                                [LocalizationKey.LinkedChatsHeader] = Row((Ru, "Привязанные чаты:"), (En, "Linked chats:"), (Es, "Chats vinculados:"), (It, "Chat collegate:"), (De, "Verknüpfte Chats:"), (Fr, "Discussions liées :"), (PtBr, "Chats vinculados:"), (Uk, "Прив'язані чати:"), (Be, "Прывязаныя чаты:")),
      [LocalizationKey.NoChats] = Row((Ru, "Чаты ещё не привязаны."), (En, "No chats linked yet."), (Es, "Aún no hay chats vinculados."), (It, "Nessuna chat collegata."), (De, "Noch keine Chats verknüpft."), (Fr, "Aucune discussion liée pour le moment."), (PtBr, "Nenhum chat vinculado ainda."), (Uk, "Чати ще не прив'язані."), (Be, "Чаты яшчэ не прывязаны.")),
      [LocalizationKey.ChatLine] = Row((Ru, "• {label}"), (En, "• {label}"), (Es, "• {label}"), (It, "• {label}"), (De, "• {label}"), (Fr, "• {label}"), (PtBr, "• {label}"), (Uk, "• {label}"), (Be, "• {label}")),
      [LocalizationKey.ChatNotLinked] = Row((Ru, "Этот чат не привязан к аккаунту. Нажмите /start, чтобы посмотреть, как его привязать."), (En, "This chat is not linked to an account. Press /start to see how to link it."), (Es, "Este chat no está vinculado a una cuenta. Pulsa /start para ver cómo vincularlo."), (It, "Questa chat non è collegata a un account. Premi /start per vedere come collegarla."), (De, "Dieser Chat ist mit keinem Konto verknüpft. Drücke /start, um zu sehen, wie du ihn verknüpfst."), (Fr, "Cette discussion n'est liée à aucun compte. Appuyez sur /start pour voir comment la lier."), (PtBr, "Este chat não está vinculado a uma conta. Pressione /start para ver como vinculá-lo."), (Uk, "Цей чат не прив'язаний до акаунту. Натисніть /start, щоб дізнатися, як його прив'язати."), (Be, "Гэты чат не прывязаны да акаўнта. Націсніце /start, каб даведацца, як яго прывязаць.")),
      [LocalizationKey.NoSubscriptions] = Row((Ru, "Подписки не найдены"), (En, "No subscriptions found"), (Es, "No se han encontrado suscripciones"), (It, "Nessun abbonamento trovato"), (De, "Keine Abonnements gefunden"), (Fr, "Aucun abonnement trouvé"), (PtBr, "Nenhuma assinatura encontrada"), (Uk, "Підписок не знайдено"), (Be, "Падпіскі не знойдзены")),
      [LocalizationKey.TemporarilyUnavailable] = Row((Ru, "Временно недоступно"), (En, "Temporarily not available"), (Es, "No disponible temporalmente"), (It, "Temporaneamente non disponibile"), (De, "Vorübergehend nicht verfügbar"), (Fr, "Temporairement indisponible"), (PtBr, "Indisponível temporariamente"), (Uk, "Тимчасово недоступно"), (Be, "Часова недаступна")),
      [LocalizationKey.UnknownIndicator] = Row((Ru, "Неизвестный индикатор"), (En, "Unknown indicator"), (Es, "Indicador desconocido"), (It, "Indicatore sconosciuto"), (De, "Unbekannter Indikator"), (Fr, "Indicateur inconnu"), (PtBr, "Indicador desconhecido"), (Uk, "Невідомий індикатор"), (Be, "Невядомы індыкатар")),
      [LocalizationKey.SubscriptionNotFound] = Row((Ru, "Подписка не найдена"), (En, "Subscription not found"), (Es, "Suscripción no encontrada"), (It, "Abbonamento non trovato"), (De, "Abonnement nicht gefunden"), (Fr, "Abonnement introuvable"), (PtBr, "Assinatura não encontrada"), (Uk, "Підписку не знайдено"), (Be, "Падпіску не знойдзена")),
      [LocalizationKey.SubscriptionListTitle] = Row((Ru, "Список подписок{pages}"), (En, "Subscription list{pages}"), (Es, "Lista de suscripciones{pages}"), (It, "Elenco abbonamenti{pages}"), (De, "Abonnementliste{pages}"), (Fr, "Liste des abonnements{pages}"), (PtBr, "Lista de assinaturas{pages}"), (Uk, "Список підписок{pages}"), (Be, "Спіс падпісак{pages}")),
      [LocalizationKey.SubscriptionsSearchHint] = Row(
        (Ru, "\n\nОтправьте тикер, чтобы отфильтровать список."),
        (En, "\n\nSend a ticker to filter the list."),
        (Es, "\n\nEnvía un ticker para filtrar la lista."),
        (It, "\n\nInvia un ticker per filtrare l'elenco."),
        (De, "\n\nSende einen Ticker, um die Liste zu filtern."),
        (Fr, "\n\nEnvoyez un ticker pour filtrer la liste."),
        (PtBr, "\n\nEnvie um ticker para filtrar a lista."),
        (Uk, "\n\nНадішліть тікер, щоб відфільтрувати список."),
        (Be, "\n\nДашліце тыкер, каб адфільтраваць спіс.")),
      [LocalizationKey.SubscriptionsFilterActive] = Row(
        (Ru, "\n\nФильтр: «{filter}» - отправьте другой тикер, чтобы изменить его."),
        (En, "\n\nFilter: \"{filter}\" - send another ticker to change it."),
        (Es, "\n\nFiltro: «{filter}» - envía otro ticker para cambiarlo."),
        (It, "\n\nFiltro: «{filter}» - invia un altro ticker per modificarlo."),
        (De, "\n\nFilter: \"{filter}\" - sende einen anderen Ticker, um ihn zu ändern."),
        (Fr, "\n\nFiltre : \"{filter}\" - envoyez un autre ticker pour le modifier."),
        (PtBr, "\n\nFiltro: \"{filter}\" - envie outro ticker para alterá-lo."),
        (Uk, "\n\nФільтр: «{filter}» - надішліть інший тікер, щоб змінити його."),
        (Be, "\n\nФільтр: \"{filter}\" - дашліце іншы тыкер, каб змяніць яго.")),
      [LocalizationKey.ChatNotLinkedOrNoKeys] = Row((Ru, "Этот чат не привязан к аккаунту или в аккаунте нет API-ключей. Нажмите /start, чтобы посмотреть, как его привязать."), (En, "This chat is not linked to an account or the account has no API keys. Press /start to see how to link it."), (Es, "Este chat no está vinculado a una cuenta o la cuenta no tiene claves API. Pulsa /start para ver cómo vincularlo."), (It, "Questa chat non è collegata a un account oppure l'account non ha chiavi API. Premi /start per vedere come collegarla."), (De, "Dieser Chat ist mit keinem Konto verknüpft oder das Konto hat keine API-Keys. Drücke /start, um zu sehen, wie du ihn verknüpfst."), (Fr, "Cette discussion n'est liée à aucun compte ou le compte n'a pas de clés API. Appuyez sur /start pour voir comment la lier."), (PtBr, "Este chat não está vinculado a uma conta ou a conta não tem chaves de API. Pressione /start para ver como vinculá-lo."), (Uk, "Цей чат не прив'язаний до акаунту або в акаунті немає API-ключів. Натисніть /start, щоб дізнатися, як його прив'язати."), (Be, "Гэты чат не прывязаны да акаўнта або ў акаўнце няма API-ключоў. Націсніце /start, каб даведацца, як яго прывязаць.")),
      [LocalizationKey.ChatLinkedToAnother] = Row((Ru, "Этот чат уже привязан к другому аккаунту. Сначала отвяжите его (веб-интерфейс → Настройки → Telegram)."), (En, "This chat is already linked to another account. Unlink it first (web interface → Settings → Telegram)."), (Es, "Este chat ya está vinculado a otra cuenta. Primero desvincúlalo (interfaz web → Ajustes → Telegram)."), (It, "Questa chat è già collegata a un altro account. Prima scollegala (interfaccia web → Impostazioni → Telegram)."), (De, "Dieser Chat ist bereits mit einem anderen Konto verknüpft. Trenne ihn zuerst (Web-Oberfläche → Einstellungen → Telegram)."), (Fr, "Cette discussion est déjà liée à un autre compte. Déliez-la d'abord (interface web → Paramètres → Telegram)."), (PtBr, "Este chat já está vinculado a outra conta. Desvincule-o primeiro (interface web → Configurações → Telegram)."), (Uk, "Цей чат уже прив'язаний до іншого акаунту. Спочатку відв'яжіть його (веб-інтерфейс → Налаштування → Telegram)."), (Be, "Гэты чат ужо прывязаны да іншага акаўнта. Спачатку адвяжыце яго (вэб-інтэрфейс → Налады → Telegram).")),
      [LocalizationKey.GroupChatNotAllowed] = Row(
        (Ru, "Этот чат нельзя привязать: бот работает только в личных чатах, группы и каналы не поддерживаются."),
        (En, "This chat cannot be linked - the bot works only in personal chats, groups and channels are not supported."),
        (Es, "Este chat no se puede vincular: el bot solo funciona en chats personales, los grupos y canales no son compatibles."),
        (It, "Questa chat non può essere collegata: il bot funziona solo nelle chat personali, gruppi e canali non sono supportati."),
        (De, "Dieser Chat kann nicht verknüpft werden - der Bot funktioniert nur in persönlichen Chats, Gruppen und Kanäle werden nicht unterstützt."),
        (Fr, "Cette discussion ne peut pas être liée : le bot ne fonctionne que dans les discussions personnelles, les groupes et les canaux ne sont pas pris en charge."),
        (PtBr, "Este chat não pode ser vinculado - o bot funciona apenas em chats pessoais, grupos e canais não são suportados."),
        (Uk, "Цей чат не можна прив'язати: бот працює лише в особистих чатах, групи та канали не підтримуються."),
        (Be, "Гэты чат нельга прывязаць: бот працуе толькі ў асабістых чатах, групы і каналы не падтрымліваюцца.")),
      [LocalizationKey.LinkCommandUsage] = Row(
        (Ru, "Отправьте токен так: /link &lt;токен&gt;.\n\nОдноразовый токен создаётся на странице настроек вашего экземпляра Stock Indicators."),
        (En, "Send the token like this: /link &lt;token&gt;.\n\nThe one-time token is created on the settings page of your Stock Indicators instance."),
        (Es, "Envía el token así: /link &lt;token&gt;.\n\nEl token de un solo uso se crea en la página de ajustes de tu instancia de Stock Indicators."),
        (It, "Invia il token così: /link &lt;token&gt;.\n\nIl token monouso viene creato nella pagina delle impostazioni della tua istanza di Stock Indicators."),
        (De, "Sende den Token so: /link &lt;token&gt;.\n\nDer einmalige Token wird auf der Seite der Einstellungen deiner Stock-Indicators-Instanz erstellt."),
        (Fr, "Envoyez le jeton comme ceci : /link &lt;jeton&gt;.\n\nLe jeton à usage unique est créé sur la page des paramètres de votre instance Stock Indicators."),
        (PtBr, "Envie o token assim: /link &lt;token&gt;.\n\nO token de uso único é criado na página de configurações da sua instância do Stock Indicators."),
        (Uk, "Надішліть токен так: /link &lt;токен&gt;.\n\nОдноразовий токен створюється на сторінці налаштувань вашого екземпляра Stock Indicators."),
        (Be, "Адпраўце токен так: /link &lt;токен&gt;.\n\nАднаразовы токен ствараецца на старонцы налад вашага экзэмпляра Stock Indicators.")),
      [LocalizationKey.LinkTokenInvalid] = Row((Ru, "Ссылка привязки недействительна: она одноразовая и живёт 15 минут.\n\nЗапросите новую в веб-интерфейсе: Настройки → Telegram."), (En, "This link is no longer valid - it is single-use and expires in 15 minutes.\n\nRequest a new one in the web interface: Settings → Telegram."), (Es, "El enlace de vinculación ya no es válido: es de un solo uso y caduca en 15 minutos.\n\nSolicita uno nuevo en la interfaz web: Ajustes → Telegram."), (It, "Il collegamento non è più valido: è monouso e scade tra 15 minuti.\n\nRichiedine uno nuovo nell'interfaccia web: Impostazioni → Telegram."), (De, "Der Verknüpfungslink ist nicht mehr gültig: er ist einmalig und läuft in 15 Minuten ab.\n\nFordere in der Web-Oberfläche einen neuen an: Einstellungen → Telegram."), (Fr, "Le lien de liaison n'est plus valide : il est à usage unique et expire en 15 minutes.\n\nDemandez-en un nouveau dans l'interface web : Paramètres → Telegram."), (PtBr, "O link de vinculação não é mais válido - é de uso único e expira em 15 minutos.\n\nSolicite um novo na interface web: Configurações → Telegram."), (Uk, "Посилання прив'язки недійсне: воно одноразове і діє 15 хвилин.\n\nЗапросіть нове у веб-інтерфейсі: Налаштування → Telegram."), (Be, "Спасылка прывязкі несапраўдная: яна аднаразовая і дзейнічае 15 хвілін.\n\nЗапытайце новую у вэб-інтэрфейсе: Налады → Telegram.")),
      [LocalizationKey.ChatLinkedConfirmation] = Row((Ru, "Привет, {name}! Чат привязан ✅\n\nМеню - кнопками ниже, язык меню меняется в Настройках."), (En, "Hello, {name}! Chat linked ✅\n\nThe menu is below; change the menu language in Settings."), (Es, "¡Hola, {name}! Chat vinculado ✅\n\nEl menú está abajo; el idioma del menú se cambia en Ajustes."), (It, "Ciao, {name}! Chat collegata ✅\n\nIl menu è qui sotto; la lingua del menu si cambia nelle Impostazioni."), (De, "Hallo, {name}! Chat verknüpft ✅\n\nDas Menü kommt unten; die Sprache des Menüs änderst du in den Einstellungen."), (Fr, "Bonjour, {name} ! Discussion liée ✅\n\nLe menu est ci-dessous ; changez la langue du menu dans Paramètres."), (PtBr, "Olá, {name}! Chat vinculado ✅\n\nO menu está abaixo; o idioma do menu é alterado em Configurações."), (Uk, "Привіт, {name}! Чат прив'язано ✅\n\nМеню - кнопками нижче, мову меню змінюється в Налаштуваннях."), (Be, "Вітаю, {name}! Чат прывязаны ✅\n\nМеню - кнопкамі ніжэй, мову меню змяняецца ў Наладах.")),
      [LocalizationKey.IntervalUnitDay] = Row((Ru, "дн."), (En, "d"), (Es, "d"), (It, "g"), (De, "d"), (Fr, "j"), (PtBr, "d"), (Uk, "дн."), (Be, "дз.")),
      [LocalizationKey.IntervalUnitHour] = Row((Ru, "ч."), (En, "h"), (Es, "h"), (It, "h"), (De, "h"), (Fr, "h"), (PtBr, "h"), (Uk, "год."), (Be, "гад.")),
      [LocalizationKey.IntervalUnitMinute] = Row((Ru, "мин."), (En, "min"), (Es, "min"), (It, "min"), (De, "min"), (Fr, "min"), (PtBr, "min"), (Uk, "хв."), (Be, "хв.")),
      [LocalizationKey.IntervalUnitSecond] = Row((Ru, "с"), (En, "s"), (Es, "s"), (It, "s"), (De, "s"), (Fr, "s"), (PtBr, "s"), (Uk, "с"), (Be, "с")),
      [LocalizationKey.RuleHeaderMet] = Row((Ru, "условие выполнено"), (En, "triggered"), (Es, "condición cumplida"), (It, "condizione soddisfatta"), (De, "Bedingung erfüllt"), (Fr, "condition remplie"), (PtBr, "condição cumprida"), (Uk, "умова виконана"), (Be, "умова выканана")),
      [LocalizationKey.RuleHeaderNotMet] = Row((Ru, "условия не выполнены"), (En, "conditions not met"), (Es, "condiciones no cumplidas"), (It, "condizioni non soddisfatte"), (De, "Bedingungen nicht erfüllt"), (Fr, "conditions non remplies"), (PtBr, "condições não cumpridas"), (Uk, "умови не виконані"), (Be, "умовы не выкананы")),
      [LocalizationKey.Triggered] = Row((Ru, "Сработал"), (En, "Triggered"), (Es, "Activada"), (It, "Scattato"), (De, "Ausgelöst"), (Fr, "Déclenchée"), (PtBr, "Disparada"), (Uk, "Спрацював"), (Be, "Спрацаваў")),
      [LocalizationKey.ConditionMet] = Row((Ru, "выполнено"), (En, "met"), (Es, "cumplida"), (It, "soddisfatta"), (De, "erfüllt"), (Fr, "remplie"), (PtBr, "cumprida"), (Uk, "виконано"), (Be, "выканана")),
      [LocalizationKey.GapPreviousLabel] = Row((Ru, "пред"), (En, "prev"), (Es, "anterior"), (It, "prec"), (De, "Vortag"), (Fr, "préc"), (PtBr, "ant."), (Uk, "папяр"), (Be, "папяр")),
      [LocalizationKey.MaTestUpward] = Row((Ru, "вверх"), (En, "upward"), (Es, "alcista"), (It, "rialzista"), (De, "aufwärts"), (Fr, "haussier"), (PtBr, "altista"), (Uk, "вгору"), (Be, "ўгару")),
      [LocalizationKey.MaTestDownward] = Row((Ru, "вниз"), (En, "downward"), (Es, "bajista"), (It, "ribassista"), (De, "abwärts"), (Fr, "baissier"), (PtBr, "baixista"), (Uk, "вниз"), (Be, "ўніз")),
      [LocalizationKey.MaTestNone] = Row((Ru, "без пересечения"), (En, "none"), (Es, "sin cruce"), (It, "nessun incrocio"), (De, "kein Kreuz"), (Fr, "pas de croisement"), (PtBr, "sem cruzamento"), (Uk, "без перетину"), (Be, "без перасячэння")),
      [LocalizationKey.VolumeAverageLabel] = Row((Ru, "среднего"), (En, "avg"), (Es, "media"), (It, "media"), (De, "Ø"), (Fr, "moy"), (PtBr, "média"), (Uk, "середнього"), (Be, "сярэдняга")),
      [LocalizationKey.OpenChangeLabel] = Row((Ru, "от открытия"), (En, "from open"), (Es, "desde la apertura"), (It, "dall'apertura"), (De, "ab Eröffnung"), (Fr, "depuis l'ouverture"), (PtBr, "da abertura"), (Uk, "від відкриття"), (Be, "ад адкрыцця")),
      [LocalizationKey.Week52AboveHigh] = Row((Ru, "выше 52-недельного хая"), (En, "above 52-wk high"), (Es, "sobre el máximo de 52 semanas"), (It, "sopra il massimo di 52 settimane"), (De, "über 52-Wochen-Hoch"), (Fr, "au-dessus du plus haut sur 52 semaines"), (PtBr, "acima da máxima de 52 semanas"), (Uk, "вище 52-тижневого максимуму"), (Be, "вышэй 52-тыднёвага максімуму")),
      [LocalizationKey.Week52BelowLow] = Row((Ru, "ниже 52-недельного лоя"), (En, "below 52-wk low"), (Es, "bajo el mínimo de 52 semanas"), (It, "sotto il minimo di 52 settimane"), (De, "unter 52-Wochen-Tief"), (Fr, "en dessous du plus bas sur 52 semaines"), (PtBr, "abaixo da mínima de 52 semanas"), (Uk, "нижче 52-тижневого мінімуму"), (Be, "ніжэй 52-тыднёвага мінімуму")),
      [LocalizationKey.Week52PullbackHigh] = Row((Ru, "откат от 52-недельного хая"), (En, "pullback from 52-wk high"), (Es, "retroceso desde el máximo de 52 semanas"), (It, "ritracciamento dal massimo di 52 settimane"), (De, "Rückgang vom 52-Wochen-Hoch"), (Fr, "repli depuis le plus haut sur 52 semaines"), (PtBr, "recuo da máxima de 52 semanas"), (Uk, "відкат від 52-тижневого максимуму"), (Be, "адкат ад 52-тыднёвага максімуму")),
      [LocalizationKey.Week52BounceLow] = Row((Ru, "отскок от 52-недельного лоя"), (En, "bounce from 52-wk low"), (Es, "rebote desde el mínimo de 52 semanas"), (It, "rimbalzo dal minimo di 52 settimane"), (De, "Anstieg vom 52-Wochen-Tief"), (Fr, "rebond depuis le plus bas sur 52 semaines"), (PtBr, "repique da mínima de 52 semanas"), (Uk, "відскок від 52-тижневого мінімуму"), (Be, "адскок ад 52-тыднёвага мінімуму")),
      [LocalizationKey.TrailingFromMax] = Row((Ru, "от максимума"), (En, "from max"), (Es, "desde el máximo"), (It, "dal massimo"), (De, "vom Hoch"), (Fr, "du plus haut"), (PtBr, "da máxima"), (Uk, "від максимуму"), (Be, "ад максімуму")),
      [LocalizationKey.TrailingFromMin] = Row((Ru, "от минимума"), (En, "from min"), (Es, "desde el mínimo"), (It, "dal minimo"), (De, "vom Tief"), (Fr, "du plus bas"), (PtBr, "da mínima"), (Uk, "від мінімуму"), (Be, "ад мінімуму")),
      // the alert message is sent with ParseMode.Html, so the header is bolded with an html tag
      // (markdown asterisks used to render literally on the phone)
      [LocalizationKey.TelegramHeader] = Row((Ru, "📊 <b>Уведомления</b>"), (En, "📊 <b>Alerts</b>"), (Es, "📊 <b>Alertas</b>"), (It, "📊 <b>Avvisi</b>"), (De, "📊 <b>Benachrichtigungen</b>"), (Fr, "📊 <b>Alertes</b>"), (PtBr, "📊 <b>Alertas</b>"), (Uk, "📊 <b>Повідомлення</b>"), (Be, "📊 <b>Апавяшчэнні</b>")),
      [LocalizationKey.TelegramOpenButton] = Row((Ru, "Открыть"), (En, "Open"), (Es, "Abrir"), (It, "Apri"), (De, "Öffnen"), (Fr, "Ouvrir"), (PtBr, "Abrir"), (Uk, "Відкрити"), (Be, "Адкрыць")),
      [LocalizationKey.ChartExample] = Row((Ru, "Пример"), (En, "Example"), (Es, "Ejemplo"), (It, "Esempio"), (De, "Beispiel"), (Fr, "Exemple"), (PtBr, "Exemplo"), (Uk, "Приклад"), (Be, "Прыклад")),
      [LocalizationKey.ChartTrigger] = Row((Ru, "Триггер"), (En, "Trigger"), (Es, "Disparo"), (It, "Attivazione"), (De, "Auslöser"), (Fr, "Déclencheur"), (PtBr, "Gatilho"), (Uk, "Тригер"), (Be, "Трыгер")),
      [LocalizationKey.ChartPreMarket] = Row((Ru, "Премаркет"), (En, "Pre-market"), (Es, "Pre-mercado"), (It, "Pre-mercato"), (De, "Vorhandel"), (Fr, "Pré-ouverture"), (PtBr, "Pré-mercado"), (Uk, "Премаркет"), (Be, "Прэмэркет")),
      [LocalizationKey.ChartAfterHours] = Row((Ru, "После закрытия"), (En, "After-hours"), (Es, "Fuera de horario"), (It, "Dopo la chiusura"), (De, "Nachhandel"), (Fr, "Après-clôture"), (PtBr, "Pós-mercado"), (Uk, "Після закриття"), (Be, "Пасля закрыцця")),
      [LocalizationKey.ChartOvernight] = Row((Ru, "Ночная сессия"), (En, "Overnight"), (Es, "Sesión nocturna"), (It, "Sessione notturna"), (De, "Nachtssitzung"), (Fr, "Session de nuit"), (PtBr, "Sessão noturna"), (Uk, "Нічна сесія"), (Be, "Начная сесія")),
      // OHLC/volume legend captions of the chart tooltip; the same compact words the web
      // legend shows (frontend i18n chart.legend), so the alert image reads identically
      [LocalizationKey.ChartLegendOpen] = Row((Ru, "Откр"), (En, "O"), (Es, "Ape."), (It, "Ap."), (De, "Eröff"), (Fr, "Ouv."), (PtBr, "Abre"), (Uk, "Відкр"), (Be, "Адкр")),
      [LocalizationKey.ChartLegendHigh] = Row((Ru, "Макс"), (En, "H"), (Es, "Máx"), (It, "Max"), (De, "Hoch"), (Fr, "Haut"), (PtBr, "Máx"), (Uk, "Макс"), (Be, "Макс")),
      [LocalizationKey.ChartLegendLow] = Row((Ru, "Мин"), (En, "L"), (Es, "Mín"), (It, "Min"), (De, "Tief"), (Fr, "Bas"), (PtBr, "Mín"), (Uk, "Мін"), (Be, "Мін")),
      [LocalizationKey.ChartLegendClose] = Row((Ru, "Закр"), (En, "C"), (Es, "Cie."), (It, "Chius"), (De, "Schluss"), (Fr, "Clôt"), (PtBr, "Fech"), (Uk, "Закр"), (Be, "Закр")),
      [LocalizationKey.ChartLegendVolume] = Row((Ru, "Объём"), (En, "Vol"), (Es, "Vol"), (It, "Vol"), (De, "Vol"), (Fr, "Vol"), (PtBr, "Vol"), (Uk, "Об'єм"), (Be, "Аб'ём")),
                                    [LocalizationKey.NewDeviceMethodGoogle] = Row(
        (Ru, "через Google"),
        (En, "via Google"),
        (Es, "mediante Google"),
        (It, "tramite Google"),
        (De, "über Google"),
        (Fr, "via Google"),
        (PtBr, "via Google"),
        (Uk, "через Google"),
        (Be, "праз Google")),
      [LocalizationKey.NewDeviceMethodTelegram] = Row(
        (Ru, "через Telegram"),
        (En, "via Telegram"),
        (Es, "mediante Telegram"),
        (It, "tramite Telegram"),
        (De, "über Telegram"),
        (Fr, "via Telegram"),
        (PtBr, "via Telegram"),
        (Uk, "через Telegram"),
        (Be, "праз Telegram")),
            // Transactional email template layer (EmailLayout): the code email is localized from
      // the request locale (the account language is unknown before login); {code} receives
      // the styled code block from EmailLayout.CodeBlock, not raw digits.
                                                                                                                  // A login code can create the account, so the footer cannot claim an existing one
            // BotFather profile texts (applied through the Bot API on every bot start, see BotProfile):
      // short description is the 1-120 char line on the profile page, welcome the empty-chat card
      [LocalizationKey.BotProfileShortDescription] = Row(
        (Ru, "Мониторинг торговых сигналов: алерт в момент срабатывания индикатора по вашему инструменту."),
        (En, "Trading signal monitoring: alerts the moment an indicator on your instrument triggers."),
        (Es, "Monitorización de señales de trading: alertas en el momento en que se activa un indicador."),
        (It, "Monitoraggio dei segnali di trading: un avviso quando un indicatore sul tuo strumento scatta."),
        (De, "Überwachung von Handelssignalen: eine Alert in dem Moment, wenn ein Indikator an deinem Instrument auslöst."),
        (Fr, "Surveillance des signaux de trading : une alerte dès qu'un indicateur se déclenche sur votre instrument."),
        (PtBr, "Monitoramento de sinais de trading: um alerta quando um indicador do seu instrumento é acionado."),
        (Uk, "Моніторинг торгових сигналів: алерт у момент спрацювання індикатора за вашим інструментом."),
        (Be, "Маніторынг гандлёвых сігналаў: апавяшчэнне ў момант спрацоўвання індыкатара па вашым інструменце.")),
      [LocalizationKey.BotProfileWelcome] = Row(
        (Ru, "Stock Indicators следит за вашими инструментами и присылает алерт, как только индикатор пересекает заданный порог. Поддержка нескольких языков, диплинки в веб-дашборд и история срабатываний."),
        (En, "Stock Indicators monitors your instruments and sends an alert the moment an indicator crosses your threshold. Supports multi language, deep links into the web dashboard, and alert history."),
        (Es, "Stock Indicators vigila tus instrumentos y envía una alerta en el momento en que un indicador cruza tu umbral. Admite varios idiomas, enlaces directos al panel web e historial de alertas."),
        (It, "Stock Indicators tiene d'occhio i tuoi strumenti e invia un avviso quando un indicatore supera la tua soglia. Supporta più lingue, deep link alla dashboard web e cronologia degli avvisi."),
        (De, "Stock Indicators überwacht deine Instrumente und sendet eine Alert, sobald ein Indikator deinen Schwellenwert kreuzt. Unterstützt mehrere Sprachen, Deep Links ins Web-Dashboard und eine Alert-Historie."),
        (Fr, "Stock Indicators surveille vos instruments et envoie une alerte dès qu'un indicateur franchit votre seuil. Prend en charge plusieurs langues, les liens profonds vers le tableau de bord web et l'historique des alertes."),
        (PtBr, "O Stock Indicators acompanha seus instrumentos e envia um alerta quando um indicador cruza seu limite. Compatível com vários idiomas, deep links para o painel web e histórico de alertas."),
        (Uk, "Stock Indicators стежить за вашими інструментами та надсилає алерт, щойно індикатор перетина заданий поріг. Підтримка кількох мов, диплінки в веб-дашборд та історія спрацювань."),
        (Be, "Stock Indicators сочыць за вашымі інструментамі і дасылае апавяшчэнне, як толькі індыкатар перасякае зададзены парог. Падтрымка некалькіх моў, дзеплінкі ў вэб-дашборд і гісторыя спрацоўванняў.")),
      // slash-command menu descriptions (the "/" menu next to the input box)
      [LocalizationKey.BotCommandStart] = Row(
        (Ru, "Показать приветствие и меню"),
        (En, "Show the welcome message and menu"),
        (Es, "Ver el mensaje de bienvenida y el menú"),
        (It, "Mostra il messaggio di benvenuto e il menu"),
        (De, "Begrüßung und Menü anzeigen"),
        (Fr, "Afficher le message de bienvenue et le menu"),
        (PtBr, "Mostrar a mensagem de boas-vindas e o menu"),
        (Uk, "Показати вітання та меню"),
        (Be, "Паказаць прывітанне і меню")),
      [LocalizationKey.BotCommandLanguage] = Row(
        (Ru, "Сменить язык бота"),
        (En, "Change the bot language"),
        (Es, "Cambiar el idioma del bot"),
        (It, "Cambia la lingua del bot"),
        (De, "Die Sprache des Bots wechseln"),
        (Fr, "Changer la langue du bot"),
        (PtBr, "Mudar o idioma do bot"),
        (Uk, "Змінити мову бота"),
        (Be, "Змяніць мову бота")),
      [LocalizationKey.BotCommandLink] = Row(
        (Ru, "Привязать чат токеном"), (En, "Link this chat with a token"), (Es, "Vincular este chat con un token"),
        (It, "Collega questa chat con un token"), (De, "Diesen Chat mit einem Token verknüpfen"), (Fr, "Lier cette discussion avec un jeton"),
        (PtBr, "Vincular este chat com um token"), (Uk, "Прив'язати чат токеном"), (Be, "Прывязаць чат токенам")),
    };

  /// <summary>
  /// The value of <paramref name="key"/> in <paramref name="language"/>, falling back to the
  /// default language when the requested one has no entry for the key.
  /// </summary>
  public static string Get(LocalizationKey key, string? language)
  {
    if (!Entries.TryGetValue(key, out var byLanguage))
      throw new InvalidOperationException($"No localized entry registered for key '{key}'.");

    return byLanguage.TryGetValue(Normalize(language), out var value) ? value : byLanguage[UserLocale.DefaultLanguage];
  }

  /// <summary>
  /// <see cref="Get"/> followed by named-placeholder formatting (<c>{name}</c> style).
  /// Literal braces in values are safe: only registered names are replaced.
  /// </summary>
  public static string GetFormatted(LocalizationKey key, string? language, params (string Name, object? Value)[] args)
  {
    var value = Get(key, language);

    foreach (var (name, argValue) in args)
      value = value.Replace("{" + name + "}", argValue?.ToString() ?? string.Empty, StringComparison.Ordinal);

    return value;
  }

  private static IReadOnlyDictionary<string, string> Row(params (string Language, string Value)[] values) =>
    values.ToDictionary(value => value.Language, value => value.Value, StringComparer.OrdinalIgnoreCase);

  private static string Normalize(string? language) =>
    UserLocale.IsSupportedLanguage(language) ? language! : UserLocale.DefaultLanguage;
}
