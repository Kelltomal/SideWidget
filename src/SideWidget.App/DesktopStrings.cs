namespace SideWidget.App;

internal static partial class DesktopLanguage
{
    // Russian source, with translations in the order defined by Languages.
    static readonly Dictionary<string, string[]> Strings = new(StringComparer.Ordinal)
    {
        ["SideMenu — открыть или выйти"] =
            ["SideMenu — open or exit", "SideMenu — 打开或退出", "SideMenu — 開くか終了", "SideMenu — öffnen oder beenden", "SideMenu — відкрити або вийти", "SideMenu — адкрыць або выйсці", "SideMenu — abrir o salir", "SideMenu — abrir ou sair"],
        ["Открыть панель"] =
            ["Open panel", "打开面板", "パネルを開く", "Panel öffnen", "Відкрити панель", "Адкрыць панэль", "Abrir panel", "Abrir painel"],
        ["Открепить"] =
            ["Unpin", "取消固定", "固定を解除", "Lösen", "Відкріпити", "Адмацаваць", "Desanclar", "Desafixar"],
        ["Закрепить"] =
            ["Pin", "固定", "固定する", "Anheften", "Закріпити", "Замацаваць", "Anclar", "Fixar"],
        ["Перезапустить"] =
            ["Restart", "重启", "再起動", "Neu starten", "Перезапустити", "Перазапусціць", "Reiniciar", "Reiniciar"],
        ["Запустить новую сборку"] =
            ["Launch newer build", "启动新版本", "新しいビルドを起動", "Neueren Build starten", "Запустити нову збірку", "Запусціць новую зборку", "Iniciar versión nueva", "Iniciar versão mais recente"],
        ["Выйти"] =
            ["Exit", "退出", "終了", "Beenden", "Вийти", "Выйсці", "Salir", "Sair"],
        ["Горячая клавиша занята. Выберите другую в настройках."] =
            ["The shortcut is in use. Choose another in Settings.", "此快捷键已被占用。请在设置中选择其他快捷键。", "このショートカットは使用中です。設定で別のキーを選んでください。", "Das Tastenkürzel wird bereits verwendet. Wählen Sie in den Einstellungen ein anderes.", "Гаряча клавіша зайнята. Виберіть іншу в налаштуваннях.", "Гарачая клавіша занятая. Выберыце іншую ў наладах.", "El atajo está en uso. Elige otro en Ajustes.", "O atalho está em uso. Escolha outro nas configurações."],
        ["Горячая клавиша занята другим приложением"] =
            ["Another app is using this shortcut.", "其他应用正在使用此快捷键。", "別のアプリがこのショートカットを使用しています。", "Eine andere App verwendet dieses Tastenkürzel.", "Інша програма використовує цю гарячу клавішу.", "Іншая праграма выкарыстоўвае гэтую гарачую клавішу.", "Otra aplicación usa este atajo.", "Outro aplicativo está usando este atalho."],
        ["Интерфейс перезапущен после ошибки WebView2"] =
            ["The interface restarted after a WebView2 error.", "界面在 WebView2 出错后已重启。", "WebView2 エラーの後、インターフェイスが再起動しました。", "Die Oberfläche wurde nach einem WebView2-Fehler neu gestartet.", "Інтерфейс перезапущено після помилки WebView2.", "Інтэрфейс перазапушчаны пасля памылкі WebView2.", "La interfaz se reinició tras un error de WebView2.", "A interface foi reiniciada após um erro do WebView2."],
        ["Не удалось запустить интерфейс WebView2.\nРаспакуйте полную сборку SideWidget в локальную папку, доступную для записи. Сохраните папку WebView2Runtime рядом с приложением.\n\nТип ошибки: {0}"] =
            ["Could not start the WebView2 interface.\nExtract the full SideWidget build to a local folder you can write to. Keep the WebView2Runtime folder next to the app.\n\nError type: {0}", "无法启动 WebView2 界面。\n请将完整的 SideWidget 版本解压到有写权限的本地文件夹，并将 WebView2Runtime 文件夹保留在应用旁边。\n\n错误类型：{0}", "WebView2 インターフェイスを起動できませんでした。\nSideWidget の完全版を書き込み可能なローカルフォルダーに展開してください。WebView2Runtime フォルダーはアプリと同じ場所に置いてください。\n\nエラーの種類: {0}", "Die WebView2-Oberfläche konnte nicht gestartet werden.\nEntpacken Sie den vollständigen SideWidget-Build in einen beschreibbaren lokalen Ordner. Lassen Sie den Ordner WebView2Runtime neben der App.\n\nFehlertyp: {0}", "Не вдалося запустити інтерфейс WebView2.\nРозпакуйте повну збірку SideWidget у локальну папку з правом запису. Залиште папку WebView2Runtime поруч із програмою.\n\nТип помилки: {0}", "Не ўдалося запусціць інтэрфейс WebView2.\nРаспакуйце поўную зборку SideWidget у лакальную папку з правам запісу. Пакіньце папку WebView2Runtime побач з праграмай.\n\nТып памылкі: {0}", "No se pudo iniciar la interfaz de WebView2.\nExtrae la versión completa de SideWidget en una carpeta local con permiso de escritura. Mantén la carpeta WebView2Runtime junto a la aplicación.\n\nTipo de error: {0}", "Não foi possível iniciar a interface do WebView2.\nExtraia a versão completa do SideWidget em uma pasta local com permissão de gravação. Mantenha a pasta WebView2Runtime ao lado do aplicativo.\n\nTipo de erro: {0}"],
        ["Не удалось выполнить действие. Проверьте параметры и повторите действие."] =
            ["Could not complete the action. Check the settings and try again.", "无法完成操作。请检查参数后重试。", "操作を完了できませんでした。設定を確認して、もう一度お試しください。", "Die Aktion konnte nicht ausgeführt werden. Prüfen Sie die Einstellungen und versuchen Sie es erneut.", "Не вдалося виконати дію. Перевірте параметри й повторіть спробу.", "Не ўдалося выканаць дзеянне. Праверце параметры і паўтарыце спробу.", "No se pudo completar la acción. Revisa los parámetros e inténtalo de nuevo.", "Não foi possível concluir a ação. Confira os parâmetros e tente novamente."],
        ["Изменить размер панели"] =
            ["Resize panel", "调整面板大小", "パネルのサイズを変更", "Panelgröße ändern", "Змінити розмір панелі", "Змяніць памер панэлі", "Cambiar tamaño del panel", "Redimensionar painel"],
        ["SideWidget — время и дата"] =
            ["SideWidget — time and date", "SideWidget — 时间和日期", "SideWidget — 時刻と日付", "SideWidget — Uhrzeit und Datum", "SideWidget — час і дата", "SideWidget — час і дата", "SideWidget — hora y fecha", "SideWidget — hora e data"],
        ["Время вышло. Нажмите, чтобы остановить сигнал"] =
            ["Time is up. Press to stop the alarm.", "时间到。点按可停止铃声。", "時間になりました。押すとアラームが止まります。", "Die Zeit ist abgelaufen. Drücken, um den Alarm zu stoppen.", "Час вийшов. Натисніть, щоб вимкнути сигнал.", "Час скончыўся. Націсніце, каб выключыць сігнал.", "Se acabó el tiempo. Pulsa para detener la alarma.", "O tempo acabou. Pressione para silenciar o alarme."],
        ["Время вышло"] =
            ["Time is up", "时间到", "時間切れ", "Zeit abgelaufen", "Час вийшов", "Час скончыўся", "Tiempo agotado", "Tempo esgotado"],
        ["Пауза"] =
            ["Paused", "已暂停", "一時停止中", "Pausiert", "Пауза", "Паўза", "En pausa", "Em pausa"],
        ["Готов к старту"] =
            ["Ready to start", "准备就绪", "開始できます", "Startbereit", "Готовий до запуску", "Гатовы да запуску", "Listo para empezar", "Pronto para iniciar"],
        ["Будильник"] =
            ["Alarm", "闹钟", "アラーム", "Wecker", "Будильник", "Будзільнік", "Alarma", "Alarme"],
        ["Таймер"] =
            ["Timer", "计时器", "タイマー", "Timer", "Таймер", "Таймер", "Temporizador", "Temporizador"],
        ["Работа"] =
            ["Work", "专注", "作業", "Arbeit", "Робота", "Праца", "Trabajo", "Trabalho"],
        ["Перерыв"] =
            ["Break", "休息", "休憩", "Pause", "Перерва", "Перапынак", "Descanso", "Pausa"],
        ["Нажмите"] =
            ["Press", "点按", "押す", "Drücken", "Натисніть", "Націсніце", "Pulsa", "Pressione"],
        ["{0}, осталось {1}"] =
            ["{0}, {1} remaining", "{0}，剩余 {1}", "{0}、残り {1}", "{0}, noch {1}", "{0}, залишилося {1}", "{0}, засталося {1}", "{0}, quedan {1}", "{0}, restam {1}"],
        ["Не удалось остановить сигнал. Проверьте свободное место и повторите."] =
            ["Could not stop the alarm. Check free disk space and try again.", "无法停止铃声。请检查磁盘剩余空间后重试。", "アラームを停止できませんでした。ディスクの空き容量を確認して、もう一度お試しください。", "Der Alarm konnte nicht gestoppt werden. Prüfen Sie den freien Speicherplatz und versuchen Sie es erneut.", "Не вдалося вимкнути сигнал. Перевірте вільне місце й повторіть спробу.", "Не ўдалося выключыць сігнал. Праверце вольнае месца і паўтарыце спробу.", "No se pudo detener la alarma. Comprueba el espacio libre y vuelve a intentarlo.", "Não foi possível silenciar o alarme. Verifique o espaço livre e tente novamente."],
        ["Не удалось открыть SideWidget. Проверьте доступ к локальным данным и перезапустите приложение. Тип ошибки: {0}"] =
            ["Could not open SideWidget. Check access to local data and restart the app. Error type: {0}", "无法打开 SideWidget。请检查本地数据访问权限，然后重启应用。错误类型：{0}", "SideWidget を開けませんでした。ローカルデータへのアクセスを確認し、アプリを再起動してください。エラーの種類: {0}", "SideWidget konnte nicht geöffnet werden. Prüfen Sie den Zugriff auf lokale Daten und starten Sie die App neu. Fehlertyp: {0}", "Не вдалося відкрити SideWidget. Перевірте доступ до локальних даних і перезапустіть програму. Тип помилки: {0}", "Не ўдалося адкрыць SideWidget. Праверце доступ да лакальных даных і перазапусціце праграму. Тып памылкі: {0}", "No se pudo abrir SideWidget. Comprueba el acceso a los datos locales y reinicia la aplicación. Tipo de error: {0}", "Não foi possível abrir o SideWidget. Verifique o acesso aos dados locais e reinicie o aplicativo. Tipo de erro: {0}"],
        ["SideWidget — ошибка запуска"] =
            ["SideWidget — startup error", "SideWidget — 启动错误", "SideWidget — 起動エラー", "SideWidget — Startfehler", "SideWidget — помилка запуску", "SideWidget — памылка запуску", "SideWidget — error de inicio", "SideWidget — erro de inicialização"],
        ["Подтвердить"] =
            ["Confirm", "确认", "確認", "Bestätigen", "Підтвердити", "Пацвердзіць", "Confirmar", "Confirmar"],
        ["Отмена"] =
            ["Cancel", "取消", "キャンセル", "Abbrechen", "Скасувати", "Скасаваць", "Cancelar", "Cancelar"],
        ["Сохранить"] =
            ["Save", "保存", "保存", "Speichern", "Зберегти", "Захаваць", "Guardar", "Salvar"],
        ["Закрыть"] =
            ["Close", "关闭", "閉じる", "Schließen", "Закрити", "Закрыць", "Cerrar", "Fechar"],
        ["Экран {0} — {1} × {2}"] =
            ["Display {0} — {1} × {2}", "显示器 {0} — {1} × {2}", "ディスプレイ {0} — {1} × {2}", "Bildschirm {0} — {1} × {2}", "Екран {0} — {1} × {2}", "Экран {0} — {1} × {2}", "Pantalla {0} — {1} × {2}", "Tela {0} — {1} × {2}"],
        ["Более свежая полная сборка не найдена."] =
            ["No newer complete build was found.", "未找到更新的完整版本。", "新しい完全版が見つかりませんでした。", "Es wurde kein neuerer vollständiger Build gefunden.", "Новішу повну збірку не знайдено.", "Навейшая поўная зборка не знойдзена.", "No se encontró una versión completa más reciente.", "Nenhuma versão completa mais recente foi encontrada."],
        ["Не удалось проверить папки сборок. Проверьте доступ к ним и повторите."] =
            ["Could not check the build folders. Check your permissions and try again.", "无法检查版本文件夹。请检查访问权限后重试。", "ビルドのフォルダーを確認できませんでした。アクセス権を確認して、もう一度お試しください。", "Die Build-Ordner konnten nicht geprüft werden. Prüfen Sie die Zugriffsrechte und versuchen Sie es erneut.", "Не вдалося перевірити папки збірок. Перевірте права доступу й повторіть спробу.", "Не ўдалося праверыць папкі зборак. Праверце правы доступу і паўтарыце спробу.", "No se pudieron comprobar las carpetas de las versiones. Revisa los permisos e inténtalo de nuevo.", "Não foi possível verificar as pastas das versões. Confira as permissões e tente novamente."],
        ["Выбрать папку"] =
            ["Select folder", "选择文件夹", "フォルダーを選択", "Ordner auswählen", "Вибрати папку", "Выбраць папку", "Elegir carpeta", "Selecionar pasta"],
        ["Выберите приложение"] =
            ["Choose an app", "选择应用", "アプリを選択", "App auswählen", "Виберіть програму", "Выберыце праграму", "Elige una aplicación", "Escolha um aplicativo"],
        ["Изображение станет значком. Теперь выберите программу или ярлык, который нужно запускать."] =
            ["The image will become the icon. Choose the app or shortcut to launch.", "此图片将用作图标。请选择要启动的应用或快捷方式。", "この画像がアイコンになります。起動するアプリまたはショートカットを選んでください。", "Das Bild wird als Symbol verwendet. Wählen Sie die App oder Verknüpfung aus, die gestartet werden soll.", "Зображення стане значком. Виберіть програму або ярлик для запуску.", "Відарыс стане значком. Выберыце праграму або цэтлік для запуску.", "La imagen se usará como icono. Elige la aplicación o el acceso directo que se abrirá.", "A imagem será usada como ícone. Escolha o aplicativo ou atalho a ser aberto."],
        ["Выберите значок PNG, ICO или SVG"] =
            ["Choose a PNG, ICO, or SVG icon.", "请选择 PNG、ICO 或 SVG 格式的图标。", "PNG、ICO、SVG のいずれかのアイコンを選んでください。", "Wählen Sie ein PNG-, ICO- oder SVG-Symbol aus.", "Виберіть значок PNG, ICO або SVG.", "Выберыце значок PNG, ICO або SVG.", "Elige un icono PNG, ICO o SVG.", "Escolha um ícone PNG, ICO ou SVG."],
        ["Значок должен быть меньше 150 КБ"] =
            ["The icon must be smaller than 150 KB.", "图标必须小于 150 KB。", "アイコンは 150 KB 未満にしてください。", "Das Symbol muss kleiner als 150 KB sein.", "Значок має бути меншим за 150 КБ.", "Значок мусіць быць меншым за 150 КБ.", "El icono debe ocupar menos de 150 KB.", "O ícone deve ter menos de 150 KB."],
        ["Файл значка пуст"] =
            ["The icon file is empty.", "图标文件为空。", "アイコンファイルが空です。", "Die Symboldatei ist leer.", "Файл значка порожній.", "Файл значка пусты.", "El archivo del icono está vacío.", "O arquivo do ícone está vazio."],
        ["Не удалось закрепить файл"] =
            ["Could not pin the file.", "无法固定此文件。", "ファイルを固定できませんでした。", "Die Datei konnte nicht angeheftet werden.", "Не вдалося закріпити файл.", "Не ўдалося замацаваць файл.", "No se pudo anclar el archivo.", "Não foi possível fixar o arquivo."],
        ["Ключ DeepL API"] =
            ["DeepL API key", "DeepL API 密钥", "DeepL API キー", "DeepL-API-Schlüssel", "Ключ DeepL API", "Ключ DeepL API", "Clave de la API de DeepL", "Chave da API do DeepL"],
        ["Тариф API"] =
            ["API plan", "API 套餐", "API プラン", "API-Tarif", "Тариф API", "Тарыф API", "Plan de la API", "Plano da API"],
        ["Текст отправляется в DeepL только по кнопке «Перевести». Ключ хранится в Windows DPAPI."] =
            ["Text is sent to DeepL only when you click Translate. Windows DPAPI protects the stored key.", "只有点击“翻译”时，文本才会发送到 DeepL。保存的密钥由 Windows DPAPI 保护。", "テキストは「翻訳」を押したときだけ DeepL に送信されます。保存したキーは Windows DPAPI で保護されます。", "Der Text wird erst beim Klick auf „Übersetzen“ an DeepL gesendet. Windows DPAPI schützt den gespeicherten Schlüssel.", "Текст надсилається в DeepL лише після натискання «Перекласти». Збережений ключ захищено Windows DPAPI.", "Тэкст дасылаецца ў DeepL толькі пасля націскання «Перакласці». Захаваны ключ абаронены Windows DPAPI.", "El texto solo se envía a DeepL al pulsar Traducir. Windows DPAPI protege la clave guardada.", "O texto só é enviado ao DeepL ao clicar em Traduzir. A chave salva é protegida pelo Windows DPAPI."],
        ["Перевод DeepL"] =
            ["DeepL translation", "DeepL 翻译", "DeepL 翻訳", "DeepL-Übersetzung", "Переклад DeepL", "Пераклад DeepL", "Traducción con DeepL", "Tradução com DeepL"],
        ["Адрес SellyBoard"] =
            ["SellyBoard address", "SellyBoard 地址", "SellyBoard のアドレス", "SellyBoard-Adresse", "Адреса SellyBoard", "Адрас SellyBoard", "Dirección de SellyBoard", "Endereço do SellyBoard"],
        ["https://ваш-сервер/"] =
            ["https://your-server/", "https://your-server/", "https://your-server/", "https://ihr-server/", "https://ваш-сервер/", "https://ваш-сервер/", "https://tu-servidor/", "https://seu-servidor/"],
        ["Персональный API-ключ"] =
            ["Personal API key", "个人 API 密钥", "個人用 API キー", "Persönlicher API-Schlüssel", "Персональний ключ API", "Персанальны ключ API", "Clave personal de la API", "Chave pessoal da API"],
        ["Создайте ключ календаря в SellyBoard: Настройки · API. Он должен быть привязан к вашему пользователю. Ключ хранится в Windows DPAPI; в интерфейс и браузер не передаётся."] =
            ["In SellyBoard, open Settings › API and create a calendar key linked to your account. Windows DPAPI protects the stored key. The interface and browser never receive it.", "在 SellyBoard 中打开“设置 › API”，创建与账户关联的日历密钥。保存的密钥由 Windows DPAPI 保护，界面和浏览器均无法获取。", "SellyBoard の「設定 › API」で、アカウントにひも付いたカレンダーキーを作成してください。保存したキーは Windows DPAPI で保護され、インターフェイスやブラウザーには送信されません。", "Öffnen Sie in SellyBoard „Einstellungen › API“ und erstellen Sie einen Kalenderschlüssel für Ihr Konto. Windows DPAPI schützt den Schlüssel. Oberfläche und Browser erhalten ihn nicht.", "У SellyBoard відкрийте «Налаштування › API» та створіть ключ календаря для свого облікового запису. Windows DPAPI захищає збережений ключ. Інтерфейс і браузер його не отримують.", "У SellyBoard адкрыйце «Налады › API» і стварыце ключ календара для свайго ўліковага запісу. Windows DPAPI абараняе захаваны ключ. Інтэрфейс і браўзер яго не атрымліваюць.", "En SellyBoard, abre Ajustes › API y crea una clave de calendario vinculada a tu cuenta. Windows DPAPI protege la clave guardada. La interfaz y el navegador no la reciben.", "No SellyBoard, abra Configurações › API e crie uma chave de calendário vinculada à sua conta. O Windows DPAPI protege a chave salva. A interface e o navegador não recebem a chave."],
        ["Подключить SellyBoard"] =
            ["Connect SellyBoard", "连接 SellyBoard", "SellyBoard に接続", "SellyBoard verbinden", "Підключити SellyBoard", "Падключыць SellyBoard", "Conectar SellyBoard", "Conectar ao SellyBoard"],
        ["Подключить"] =
            ["Connect", "连接", "接続", "Verbinden", "Підключити", "Падключыць", "Conectar", "Conectar"],
        ["SideWidget — печать QR-стикеров"] =
            ["SideWidget — print QR labels", "SideWidget — 打印二维码标签", "SideWidget — QR ラベルの印刷", "SideWidget — QR-Etiketten drucken", "SideWidget — друк QR-наліпок", "SideWidget — друк QR-наліпак", "SideWidget — imprimir etiquetas QR", "SideWidget — imprimir etiquetas QR"],
        ["Печать…"] =
            ["Print…", "打印…", "印刷…", "Drucken…", "Друк…", "Друкаваць…", "Imprimir…", "Imprimir…"],
        ["Документ печати слишком большой. Уменьшите количество стикеров."] =
            ["The print document is too large. Use fewer labels.", "打印文档过大。请减少标签数量。", "印刷する文書が大きすぎます。ラベルの数を減らしてください。", "Das Druckdokument ist zu groß. Verringern Sie die Anzahl der Etiketten.", "Документ для друку завеликий. Зменште кількість наліпок.", "Дакумент для друку завялікі. Паменшыце колькасць наліпак.", "El documento de impresión es demasiado grande. Reduce la cantidad de etiquetas.", "O documento de impressão é grande demais. Reduza o número de etiquetas."],
        ["Не удалось открыть диалог печати. Проверьте принтер и нажмите «Печать…» ещё раз."] =
            ["Could not open the print dialog. Check the printer and click Print… again.", "无法打开打印对话框。请检查打印机，然后再次点击“打印…”。", "印刷ダイアログを開けませんでした。プリンターを確認して、もう一度「印刷…」を押してください。", "Der Druckdialog konnte nicht geöffnet werden. Prüfen Sie den Drucker und klicken Sie erneut auf „Drucken…“.", "Не вдалося відкрити діалог друку. Перевірте принтер і ще раз натисніть «Друк…».", "Не ўдалося адкрыць дыялог друку. Праверце прынтар і зноў націсніце «Друкаваць…».", "No se pudo abrir el cuadro de impresión. Comprueba la impresora y vuelve a pulsar «Imprimir…».", "Não foi possível abrir a janela de impressão. Verifique a impressora e clique em «Imprimir…» novamente."],
        ["Не удалось подготовить стикеры. Повторите печать."] =
            ["Could not prepare the labels. Try printing again.", "无法准备标签。请重新打印。", "ラベルを準備できませんでした。印刷をやり直してください。", "Die Etiketten konnten nicht vorbereitet werden. Starten Sie den Druck erneut.", "Не вдалося підготувати наліпки. Повторіть друк.", "Не ўдалося падрыхтаваць наліпкі. Паўтарыце друк.", "No se pudieron preparar las etiquetas. Intenta imprimir de nuevo.", "Não foi possível preparar as etiquetas. Tente imprimir novamente."],
        ["Не удалось открыть печать. Проверьте WebView2 и повторите."] =
            ["Could not open the printing window. Check WebView2 and try again.", "无法打开打印窗口。请检查 WebView2 后重试。", "印刷ウィンドウを開けませんでした。WebView2 を確認して、もう一度お試しください。", "Das Druckfenster konnte nicht geöffnet werden. Prüfen Sie WebView2 und versuchen Sie es erneut.", "Не вдалося відкрити вікно друку. Перевірте WebView2 і повторіть спробу.", "Не ўдалося адкрыць акно друку. Праверце WebView2 і паўтарыце спробу.", "No se pudo abrir la ventana de impresión. Comprueba WebView2 e inténtalo de nuevo.", "Não foi possível abrir a janela de impressão. Verifique o WebView2 e tente novamente."],
        ["Не удалось сохранить размер панели"] =
            ["Could not save the panel size.", "无法保存面板大小。", "パネルのサイズを保存できませんでした。", "Die Panelgröße konnte nicht gespeichert werden.", "Не вдалося зберегти розмір панелі.", "Не ўдалося захаваць памер панэлі.", "No se pudo guardar el tamaño del panel.", "Não foi possível salvar o tamanho do painel."],
        ["Не удалось сохранить положение панели"] =
            ["Could not save the panel position.", "无法保存面板位置。", "パネルの位置を保存できませんでした。", "Die Position des Panels konnte nicht gespeichert werden.", "Не вдалося зберегти положення панелі.", "Не ўдалося захаваць становішча панэлі.", "No se pudo guardar la posición del panel.", "Não foi possível salvar a posição do painel."],
        ["Не удалось сохранить таймер. Проверьте свободное место."] =
            ["Could not save the timer. Check free disk space.", "无法保存计时器。请检查磁盘剩余空间。", "タイマーを保存できませんでした。ディスクの空き容量を確認してください。", "Der Timer konnte nicht gespeichert werden. Prüfen Sie den freien Speicherplatz.", "Не вдалося зберегти таймер. Перевірте вільне місце.", "Не ўдалося захаваць таймер. Праверце вольнае месца.", "No se pudo guardar el temporizador. Comprueba el espacio libre.", "Não foi possível salvar o temporizador. Verifique o espaço livre."],
        ["Наведите на окно"] =
            ["Point to an app window", "将鼠标移到应用窗口上", "アプリのウィンドウにカーソルを合わせる", "Auf ein App-Fenster zeigen", "Наведіть на вікно", "Навядзіце на акно", "Coloca el cursor sobre una ventana", "Aponte para uma janela"],
        ["SideWidget: выбор"] =
            ["SideWidget: picker", "SideWidget：选择", "SideWidget: 選択", "SideWidget: Auswahl", "SideWidget: вибір", "SideWidget: выбар", "SideWidget: selector", "SideWidget: seleção"],
        ["SideWidget: контур выбора"] =
            ["SideWidget: selection outline", "SideWidget：选择边框", "SideWidget: 選択枠", "SideWidget: Auswahlrahmen", "SideWidget: контур вибору", "SideWidget: контур выбару", "SideWidget: contorno de selección", "SideWidget: contorno da seleção"],
        ["Недоступно"] =
            ["Unavailable", "不可用", "利用不可", "Nicht verfügbar", "Недоступно", "Недаступна", "No disponible", "Indisponível"],
        ["Левая кнопка"] =
            ["Left click", "单击左键", "左クリック", "Linksklick", "Ліва кнопка", "Левая кнопка", "Clic izquierdo", "Clique esquerdo"],
        ["Выбрать цвет"] =
            ["Pick color", "选取颜色", "色を選択", "Farbe auswählen", "Вибрати колір", "Выбраць колер", "Elegir color", "Selecionar cor"],
        ["Esc / правая кнопка"] =
            ["Esc / right click", "Esc / 单击右键", "Esc / 右クリック", "Esc / Rechtsklick", "Esc / права кнопка", "Esc / правая кнопка", "Esc / clic derecho", "Esc / clique direito"],
        ["Отмена · 30 с"] =
            ["Cancel · 30 sec", "取消 · 30 秒", "キャンセル · 30 秒", "Abbrechen · 30 s", "Скасувати · 30 с", "Скасаваць · 30 с", "Cancelar · 30 s", "Cancelar · 30 s"],
        ["Окно недоступно для закрепления"] =
            ["This window can't be pinned", "无法固定此窗口", "このウィンドウは固定できません", "Dieses Fenster kann nicht angeheftet werden", "Це вікно неможливо закріпити", "Гэта акно нельга замацаваць", "No se puede anclar esta ventana", "Esta janela não pode ser fixada"],
        ["Левая кнопка — выбрать окно"] =
            ["Left click to select a window", "单击左键选择窗口", "左クリックでウィンドウを選択", "Linksklick zum Auswählen eines Fensters", "Ліва кнопка — вибрати вікно", "Левая кнопка — выбраць акно", "Clic izquierdo para elegir una ventana", "Clique esquerdo para escolher uma janela"],
        ["Esc / правая кнопка — отмена · 30 с"] =
            ["Esc / right click to cancel · 30 sec", "Esc / 单击右键取消 · 30 秒", "Esc / 右クリックでキャンセル · 30 秒", "Esc / Rechtsklick: Abbrechen · 30 s", "Esc / права кнопка — скасувати · 30 с", "Esc / правая кнопка — скасаваць · 30 с", "Esc / clic derecho para cancelar · 30 s", "Esc / clique direito para cancelar · 30 s"],
        ["приложение"] =
            ["app", "应用", "アプリ", "App", "програма", "праграма", "aplicación", "aplicativo"],
        ["Избранные приложения"] =
            ["Pinned apps", "固定的应用", "固定したアプリ", "Angeheftete Apps", "Закріплені програми", "Замацаваныя праграмы", "Aplicaciones ancladas", "Aplicativos fixados"],
        ["Изменить закрепление «{0}»? Уже закреплённое приложение будет убрано. Изменение можно отменить в разделе «Приложения»."] =
            ["Toggle pinning for “{0}”? If it is pinned, it will be removed from the list. You can undo this in Apps.", "更改“{0}”的固定状态？已固定的应用会从列表中移除。可在“应用”中撤销。", "「{0}」の固定状態を切り替えますか？固定済みの場合は一覧から削除されます。「アプリ」で元に戻せます。", "Anheftung für „{0}“ ändern? Ist die App angeheftet, wird sie aus der Liste entfernt. Unter „Apps“ können Sie die Änderung rückgängig machen.", "Змінити закріплення «{0}»? Закріплену програму буде вилучено зі списку. Зміну можна скасувати в розділі «Програми».", "Змяніць замацаванне «{0}»? Замацаваная праграма будзе выдалена са спіса. Змену можна скасаваць у раздзеле «Праграмы».", "¿Cambiar el anclaje de «{0}»? Si ya está anclada, desaparecerá de la lista. Puedes deshacer el cambio en Aplicaciones.", "Alterar a fixação de “{0}”? Se já estiver fixado, o aplicativo será removido da lista. Você pode desfazer a alteração em Aplicativos."],
        ["Очистить историю?"] =
            ["Clear history?", "清除历史记录？", "履歴を消去しますか？", "Verlauf löschen?", "Очистити історію?", "Ачысціць гісторыю?", "¿Borrar el historial?", "Limpar histórico?"],
        ["Все сохранённые вычисления будут удалены. Остальные данные останутся."] =
            ["All saved calculations will be deleted. Your other data will be kept.", "所有已保存的计算都会被删除，其他数据会保留。", "保存した計算がすべて削除されます。ほかのデータは保持されます。", "Alle gespeicherten Berechnungen werden gelöscht. Andere Daten bleiben erhalten.", "Усі збережені обчислення буде видалено. Інші дані залишаться.", "Усе захаваныя вылічэнні будуць выдаленыя. Астатнія даныя застануцца.", "Se eliminarán todos los cálculos guardados. El resto de los datos se conservará.", "Todos os cálculos salvos serão excluídos. Seus outros dados serão mantidos."],
        ["Изменить значок папки?"] =
            ["Change the folder icon?", "更改文件夹图标？", "フォルダーのアイコンを変更しますか？", "Ordnersymbol ändern?", "Змінити значок папки?", "Змяніць значок папкі?", "¿Cambiar el icono de la carpeta?", "Alterar o ícone da pasta?"],
        ["{0}\n\nНовый значок — «{1}»."] =
            ["{0}\n\nNew icon: “{1}”.", "{0}\n\n新图标：“{1}”。", "{0}\n\n新しいアイコン:「{1}」。", "{0}\n\nNeues Symbol: „{1}“.", "{0}\n\nНовий значок — «{1}».", "{0}\n\nНовы значок — «{1}».", "{0}\n\nNuevo icono: «{1}».", "{0}\n\nNovo ícone: “{1}”."],
        ["Выполнить системное действие?"] =
            ["Perform a system action?", "执行系统操作？", "システム操作を実行しますか？", "Systemaktion ausführen?", "Виконати системну дію?", "Выканаць сістэмнае дзеянне?", "¿Realizar una acción del sistema?", "Executar uma ação do sistema?"],
        ["Перевести компьютер в сон? Сохраните текущую работу."] =
            ["Put the computer to sleep? Save your work first.", "让电脑进入睡眠？请先保存当前工作。", "PC をスリープ状態にしますか？作業を保存してください。", "Den Computer in den Energiesparmodus versetzen? Speichern Sie Ihre Arbeit.", "Перевести комп’ютер у режим сну? Збережіть поточну роботу.", "Перавесці камп’ютар у рэжым сну? Захавайце бягучую працу.", "¿Suspender el equipo? Guarda primero tu trabajo.", "Suspender o computador? Salve seu trabalho primeiro."],
        ["Выключить компьютер? Сохраните документы. Приложения не будут принудительно завершены."] =
            ["Shut down the computer? Save your documents. Apps will close normally.", "关闭电脑？请保存文档。应用会正常关闭。", "PC をシャットダウンしますか？文書を保存してください。アプリは通常どおり終了します。", "Den Computer herunterfahren? Speichern Sie Ihre Dokumente. Apps werden normal beendet.", "Вимкнути комп’ютер? Збережіть документи. Програми закриються звичайним способом.", "Выключыць камп’ютар? Захавайце дакументы. Праграмы закрыюцца звычайным чынам.", "¿Apagar el equipo? Guarda tus documentos. Las aplicaciones se cerrarán normalmente.", "Desligar o computador? Salve seus documentos. Os aplicativos serão fechados normalmente."],
        ["Перезагрузить компьютер? Сохраните документы. Приложения не будут принудительно завершены."] =
            ["Restart the computer? Save your documents. Apps will close normally.", "重启电脑？请保存文档。应用会正常关闭。", "PC を再起動しますか？文書を保存してください。アプリは通常どおり終了します。", "Den Computer neu starten? Speichern Sie Ihre Dokumente. Apps werden normal beendet.", "Перезавантажити комп’ютер? Збережіть документи. Програми закриються звичайним способом.", "Перазапусціць камп’ютар? Захавайце дакументы. Праграмы закрыюцца звычайным чынам.", "¿Reiniciar el equipo? Guarda tus documentos. Las aplicaciones se cerrarán normalmente.", "Reiniciar o computador? Salve seus documentos. Os aplicativos serão fechados normalmente."],
        ["Заблокировать сеанс Windows?"] =
            ["Lock the Windows session?", "锁定 Windows 会话？", "Windows セッションをロックしますか？", "Die Windows-Sitzung sperren?", "Заблокувати сеанс Windows?", "Заблакаваць сеанс Windows?", "¿Bloquear la sesión de Windows?", "Bloquear a sessão do Windows?"],
        ["Выбор цвета отменён"] =
            ["Color selection canceled.", "已取消颜色选择。", "色の選択をキャンセルしました。", "Farbauswahl abgebrochen.", "Вибір кольору скасовано.", "Выбар колеру скасаваны.", "Se canceló la selección del color.", "Seleção de cor cancelada."],
        ["Выбор окна отменён"] =
            ["Window selection canceled.", "已取消窗口选择。", "ウィンドウの選択をキャンセルしました。", "Fensterauswahl abgebrochen.", "Вибір вікна скасовано.", "Выбар акна скасаваны.", "Se canceló la selección de la ventana.", "Seleção de janela cancelada."],
        ["Выбор уже открыт."] =
            ["A picker is already open.", "选择器已打开。", "選択画面はすでに開いています。", "Ein Auswahlfenster ist bereits geöffnet.", "Вибір уже відкрито.", "Выбар ужо адкрыты.", "El selector ya está abierto.", "O seletor já está aberto."],
        ["Цвет этой области экрана недоступен."] =
            ["The color of this screen region is unavailable.", "无法获取此屏幕区域的颜色。", "この画面領域の色は取得できません。", "Die Farbe dieses Bildschirmbereichs ist nicht verfügbar.", "Колір цієї ділянки екрана недоступний.", "Колер гэтай вобласці экрана недаступны.", "El color de esta área de la pantalla no está disponible.", "A cor dessa área da tela está indisponível."],
        ["Выберите окно приложения, а не рабочий стол или SideWidget."] =
            ["Choose an app window. The desktop and SideWidget are excluded.", "请选择应用窗口。桌面和 SideWidget 无法选择。", "アプリのウィンドウを選んでください。デスクトップと SideWidget は対象外です。", "Wählen Sie ein App-Fenster aus. Der Desktop und SideWidget sind ausgeschlossen.", "Виберіть вікно програми. Робочий стіл і SideWidget вибрати неможливо.", "Выберыце акно праграмы. Працоўны стол і SideWidget выбраць немагчыма.", "Elige una ventana de una aplicación. El escritorio y SideWidget están excluidos.", "Escolha a janela de um aplicativo. A área de trabalho e o SideWidget ficam de fora."],
        ["Системная поверхность не является приложением. Выберите другое окно."] =
            ["This is a Windows system surface. Choose another window.", "这是 Windows 系统界面。请选择其他窗口。", "これは Windows のシステム画面です。ほかのウィンドウを選んでください。", "Dies ist eine Windows-Systemoberfläche. Wählen Sie ein anderes Fenster aus.", "Це системна поверхня Windows. Виберіть інше вікно.", "Гэта сістэмная паверхня Windows. Выберыце іншае акно.", "Esta es una superficie del sistema Windows. Elige otra ventana.", "Esta é uma área do sistema Windows. Escolha outra janela."],
        ["Сам SideWidget нельзя выбрать этим способом."] =
            ["SideWidget cannot be selected this way.", "无法以此方式选择 SideWidget。", "この方法では SideWidget を選択できません。", "SideWidget kann auf diese Weise nicht ausgewählt werden.", "SideWidget не можна вибрати в цей спосіб.", "SideWidget нельга выбраць такім чынам.", "SideWidget no se puede seleccionar de esta forma.", "O SideWidget não pode ser selecionado dessa forma."],
        ["Нет доступа к выбранному приложению. Используйте ручной выбор файла."] =
            ["The selected app could not be accessed. Choose its file manually.", "无法访问所选应用。请手动选择其文件。", "選んだアプリにアクセスできませんでした。ファイルを手動で選んでください。", "Auf die gewählte App konnte nicht zugegriffen werden. Wählen Sie ihre Datei manuell aus.", "Не вдалося отримати доступ до вибраної програми. Виберіть її файл вручну.", "Не ўдалося атрымаць доступ да выбранай праграмы. Выберыце яе файл уручную.", "No se pudo acceder a la aplicación seleccionada. Elige su archivo manualmente.", "Não foi possível acessar o aplicativo selecionado. Escolha o arquivo manualmente."],
        ["Не удалось определить приложение. Используйте ручной выбор файла."] =
            ["The app could not be identified. Choose its file manually.", "无法识别应用。请手动选择其文件。", "アプリを特定できませんでした。ファイルを手動で選んでください。", "Die App konnte nicht ermittelt werden. Wählen Sie ihre Datei manuell aus.", "Не вдалося визначити програму. Виберіть її файл вручну.", "Не ўдалося вызначыць праграму. Выберыце яе файл уручную.", "No se pudo identificar la aplicación. Elige su archivo manualmente.", "Não foi possível identificar o aplicativo. Escolha o arquivo manualmente."],
        ["Окно принадлежит системному хосту. Выберите приложение вручную; хост закреплён не будет."] =
            ["This window belongs to a system host. Choose the app manually to pin it.", "此窗口属于系统宿主。请手动选择要固定的应用。", "このウィンドウはシステムホストに属します。固定するアプリを手動で選んでください。", "Dieses Fenster gehört zu einem Systemhost. Wählen Sie die App zum Anheften manuell aus.", "Вікно належить системному хосту. Виберіть програму для закріплення вручну.", "Акно належыць сістэмнаму хосту. Выберыце праграму для замацавання ўручную.", "Esta ventana pertenece a un proceso del sistema. Elige manualmente la aplicación que quieres anclar.", "Esta janela pertence a um processo do sistema. Escolha manualmente o aplicativo que deseja fixar."],
        ["Не удалось определить идентификатор приложения Microsoft Store. Выберите его вручную."] =
            ["The Microsoft Store app ID could not be identified. Choose the app manually.", "无法识别 Microsoft Store 应用 ID。请手动选择应用。", "Microsoft Store アプリの ID を特定できませんでした。アプリを手動で選んでください。", "Die ID der Microsoft Store-App konnte nicht ermittelt werden. Wählen Sie die App manuell aus.", "Не вдалося визначити ідентифікатор програми Microsoft Store. Виберіть програму вручну.", "Не ўдалося вызначыць ідэнтыфікатар праграмы Microsoft Store. Выберыце праграму ўручную.", "No se pudo identificar el ID de la aplicación de Microsoft Store. Elige la aplicación manualmente.", "Não foi possível identificar a ID do aplicativo da Microsoft Store. Escolha o aplicativo manualmente."],
        ["Не удалось надёжно определить способ запуска приложения. Выберите файл вручную."] =
            ["The app's launch method could not be determined. Choose its file manually.", "无法确定应用的启动方式。请手动选择文件。", "アプリの起動方法を特定できませんでした。ファイルを手動で選んでください。", "Die Startmethode der App konnte nicht ermittelt werden. Wählen Sie ihre Datei manuell aus.", "Не вдалося визначити спосіб запуску програми. Виберіть файл вручну.", "Не ўдалося вызначыць спосаб запуску праграмы. Выберыце файл уручную.", "No se pudo determinar cómo iniciar la aplicación. Elige su archivo manualmente.", "Não foi possível determinar como iniciar o aplicativo. Escolha o arquivo manualmente."],
        ["Исполняемый файл выбранного окна недоступен."] =
            ["The executable for the selected window is unavailable.", "所选窗口的可执行文件不可用。", "選んだウィンドウの実行ファイルを利用できません。", "Die ausführbare Datei des ausgewählten Fensters ist nicht verfügbar.", "Виконуваний файл вибраного вікна недоступний.", "Выканальны файл выбранага акна недаступны.", "El archivo ejecutable de la ventana seleccionada no está disponible.", "O arquivo executável da janela selecionada está indisponível."],
        ["Действие не поддерживается"] =
            ["This action is unsupported.", "不支持此操作。", "この操作には対応していません。", "Diese Aktion wird nicht unterstützt.", "Ця дія не підтримується.", "Гэта дзеянне не падтрымліваецца.", "Esta acción no está disponible.", "Esta ação não é compatível."],
        ["Выберите язык приложения."] =
            ["Choose an app language.", "请选择应用语言。", "アプリの言語を選んでください。", "Wählen Sie eine App-Sprache aus.", "Виберіть мову програми.", "Выберыце мову праграмы.", "Elige el idioma de la aplicación.", "Escolha o idioma do aplicativo."],
        ["Выберите существующий локальный файл"] =
            ["Choose an existing local file.", "请选择现有的本地文件。", "既存のローカルファイルを選んでください。", "Wählen Sie eine vorhandene lokale Datei aus.", "Виберіть наявний локальний файл.", "Выберыце існы лакальны файл.", "Elige un archivo local existente.", "Escolha um arquivo local existente."],
        ["Нужна программа .exe или ярлык .lnk"] =
            ["Choose an .exe program or .lnk shortcut.", "请选择 .exe 程序或 .lnk 快捷方式。", ".exe ファイルか .lnk ショートカットを選んでください。", "Wählen Sie ein .exe-Programm oder eine .lnk-Verknüpfung aus.", "Виберіть програму .exe або ярлик .lnk.", "Выберыце праграму .exe або цэтлік .lnk.", "Elige un programa .exe o un acceso directo .lnk.", "Escolha um programa .exe ou um atalho .lnk."],
        ["Можно закрепить до 40 приложений"] =
            ["You can pin up to 40 apps.", "最多可固定 40 个应用。", "最大 40 個のアプリを固定できます。", "Sie können bis zu 40 Apps anheften.", "Можна закріпити до 40 програм.", "Можна замацаваць да 40 праграм.", "Puedes anclar hasta 40 aplicaciones.", "Você pode fixar até 40 aplicativos."],
        ["Приложение не найдено"] =
            ["App not found.", "未找到应用。", "アプリが見つかりませんでした。", "App nicht gefunden.", "Програму не знайдено.", "Праграма не знойдзена.", "No se encontró la aplicación.", "Aplicativo não encontrado."],
        ["Файл перемещён или удалён. Закрепите его заново."] =
            ["The file was moved or deleted. Pin it again.", "文件已移动或删除。请重新固定。", "ファイルが移動または削除されました。固定し直してください。", "Die Datei wurde verschoben oder gelöscht. Heften Sie sie erneut an.", "Файл переміщено або видалено. Закріпіть його знову.", "Файл перамешчаны або выдалены. Замацуйце яго нанова.", "El archivo se movió o se eliminó. Vuelve a anclarlo.", "O arquivo foi movido ou excluído. Fixe-o novamente."],
        ["Файл больше не существует"] =
            ["The file no longer exists.", "文件已不存在。", "ファイルはすでに存在しません。", "Die Datei existiert nicht mehr.", "Файл більше не існує.", "Файл больш не існуе.", "El archivo ya no existe.", "O arquivo não existe mais."],
        ["Сетевые пути открывайте в Проводнике самостоятельно"] =
            ["Open network paths directly in File Explorer.", "请直接在文件资源管理器中打开网络路径。", "ネットワークパスはエクスプローラーで直接開いてください。", "Öffnen Sie Netzwerkpfade direkt im Datei-Explorer.", "Відкривайте мережеві шляхи безпосередньо в Провіднику.", "Адкрывайце сеткавыя шляхі непасрэдна ў Правадыру.", "Abre las rutas de red directamente en el Explorador de archivos.", "Abra caminhos de rede diretamente no Explorador de Arquivos."],
        ["Обновите поиск перед открытием"] =
            ["Refresh the search before opening the result.", "请刷新搜索后再打开结果。", "検索結果を開く前に、検索を更新してください。", "Aktualisieren Sie die Suche vor dem Öffnen des Ergebnisses.", "Оновіть пошук, перш ніж відкривати результат.", "Абнавіце пошук перад адкрыццём выніку.", "Actualiza la búsqueda antes de abrir el resultado.", "Atualize a pesquisa antes de abrir o resultado."],
        ["Проверьте обновления ещё раз."] =
            ["Check for updates again.", "请重新检查更新。", "もう一度更新を確認してください。", "Suchen Sie erneut nach Updates.", "Перевірте оновлення ще раз.", "Праверце абнаўленні яшчэ раз.", "Vuelve a buscar actualizaciones.", "Verifique se há atualizações novamente."],
        ["Откройте релиз на GitHub, чтобы скачать архив."] =
            ["Open the GitHub release to download the archive.", "请打开 GitHub 发布页面下载压缩包。", "GitHub のリリースページを開いてアーカイブをダウンロードしてください。", "Öffnen Sie die GitHub-Veröffentlichung, um das Archiv herunterzuladen.", "Відкрийте випуск на GitHub, щоб завантажити архів.", "Адкрыйце выпуск на GitHub, каб спампаваць архіў.", "Abre la publicación en GitHub para descargar el archivo.", "Abra a versão no GitHub para baixar o arquivo."],
        ["Обновление уже загружается."] =
            ["The update is already downloading.", "更新已在下载中。", "更新はすでにダウンロード中です。", "Das Update wird bereits heruntergeladen.", "Оновлення вже завантажується.", "Абнаўленне ўжо спампоўваецца.", "La actualización ya se está descargando.", "A atualização já está sendo baixada."],
        ["Не удалось установить обновление. Повторите попытку или скачайте архив с GitHub."] =
            ["Could not install the update. Try again or download the archive from GitHub.", "无法安装更新。请重试，或从 GitHub 下载压缩包。", "更新をインストールできませんでした。もう一度試すか、GitHub からアーカイブをダウンロードしてください。", "Das Update konnte nicht installiert werden. Versuchen Sie es erneut oder laden Sie das Archiv von GitHub herunter.", "Не вдалося встановити оновлення. Повторіть спробу або завантажте архів із GitHub.", "Не ўдалося ўсталяваць абнаўленне. Паўтарыце спробу або спампуйце архіў з GitHub.", "No se pudo instalar la actualización. Inténtalo de nuevo o descarga el archivo de GitHub.", "Não foi possível instalar a atualização. Tente novamente ou baixe o arquivo no GitHub."],
        ["Укажите целое число минут или секунд"] =
            ["Enter a whole number of minutes or seconds.", "请输入整数分钟或秒数。", "分または秒を整数で入力してください。", "Geben Sie ganze Minuten oder Sekunden ein.", "Укажіть цілу кількість хвилин або секунд.", "Укажыце цэлую колькасць хвілін або секунд.", "Introduce un número entero de minutos o segundos.", "Informe um número inteiro de minutos ou segundos."],
        ["Ожидалось изображение PNG или JPEG"] =
            ["A PNG or JPEG image is required.", "需要 PNG 或 JPEG 图像。", "PNG または JPEG 画像が必要です。", "Ein PNG- oder JPEG-Bild ist erforderlich.", "Потрібне зображення PNG або JPEG.", "Патрэбны відарыс PNG або JPEG.", "Se requiere una imagen PNG o JPEG.", "É necessária uma imagem PNG ou JPEG."],
        ["Повреждённое изображение"] =
            ["The image is corrupted.", "图像已损坏。", "画像が破損しています。", "Das Bild ist beschädigt.", "Зображення пошкоджено.", "Відарыс пашкоджаны.", "La imagen está dañada.", "A imagem está corrompida."],
        ["SVG содержит неподдерживаемые элементы"] =
            ["The SVG contains unsupported elements.", "SVG 含有不支持的元素。", "SVG に対応していない要素が含まれています。", "Die SVG-Datei enthält nicht unterstützte Elemente.", "SVG містить непідтримувані елементи.", "SVG змяшчае элементы, якія не падтрымліваюцца.", "El SVG contiene elementos no compatibles.", "O SVG contém elementos incompatíveis."],
        ["Внешние ресурсы SVG не разрешены"] =
            ["The SVG cannot reference external resources.", "SVG 不得引用外部资源。", "SVG で外部リソースは参照できません。", "Die SVG-Datei darf keine externen Ressourcen referenzieren.", "SVG не може посилатися на зовнішні ресурси.", "SVG не можа спасылацца на вонкавыя рэсурсы.", "El SVG no puede hacer referencia a recursos externos.", "O SVG não pode referenciar recursos externos."],
        ["Выберите SVG, PNG или JPEG"] =
            ["Choose SVG, PNG, or JPEG.", "请选择 SVG、PNG 或 JPEG。", "SVG、PNG、JPEG のいずれかを選んでください。", "Wählen Sie SVG, PNG oder JPEG aus.", "Виберіть SVG, PNG або JPEG.", "Выберыце SVG, PNG або JPEG.", "Elige SVG, PNG o JPEG.", "Escolha SVG, PNG ou JPEG."],
    };
}
