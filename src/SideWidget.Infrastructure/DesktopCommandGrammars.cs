namespace SideWidget.Infrastructure;

static partial class DesktopCommandLanguages
{
    // Subject and icon aliases follow the order of Subjects and Icons.
    static Spec[] Specs() =>
    [
        // English
        new(
            "invoices,invoice,bills,bill|screenshots,screenshot,screen captures|documents,document,docs|images,image,photos,photo,pictures,picture|archives,archive,zip files,compressed files|logs,log,log files|videos,video|media,media files|games,game|programs,program,apps,applications|scripts,script,batch files|server launches,server shortcuts,servers|all files,files,desktop",
            "photo,photos,image,images,pictures|document,documents,docs|finance,finances,bills,invoices|legal,contracts,agreements|archive,archives,zip|music,audio|video,videos|apps,applications,programs|games,game|server,servers|logs,log,journal|mixed,miscellaneous",
            "and",
            """(?:(?:the|my)\s+)?(?:desktop\s+)?""",
            "please[ ,]+",
            """\s+(?:and|but|then|while)\s+(?!(?:the\s+)?{subject-start}(?:\s|$))""",
            """(?:(?:(?:sort|organize|organise|group|arrange|categorize)\s+{subjects}\s+by\s+(?:(?<year>year|years)|(?<type>type|types|file type|file types|category|categories)|(?<topic>topic|topics|subject|theme))|(?:move|put|place|sort|organize|organise)\s+{subjects}\s+(?:into|to|in)\s+(?:(?<separate>(?:a\s+)?separate\s+folder)|(?:(?:the\s+)?folder\s+)?{folder}))|(?:sort|organize|organise|group|arrange|categorize)\s+{subjects}(?<type>))""",
            """(?:leave|keep|exclude|do not move|don't move)\s+{subjects}(?:\s+(?:untouched|in place|where they are|as is|on (?:the )?desktop|on (?:the )?desktop unchanged))?""",
            """(?:change|replace|set|pick|choose|suggest|update|customize)\s+(?:(?:the|a)\s+)?(?:desktop\s+)?(?:(?:folder|folders')\s+)?icons?(?:\s+(?:for|of)\s+(?:the\s+)?folders?)?(?:\s+on\s+(?:the\s+)?desktop)?""",
            """(?:change|replace|set|use)\s+(?:(?:the\s+)?(?:selected\s+)?(?:folder\s+icon|icon\s+(?:of|for)\s+(?:the\s+)?folder)\s+(?:to|with|as)\s+(?:a\s+)?{icon}(?:\s+icon)?|(?:a\s+|the\s+)?{icon}\s+icon\s+(?:for|on)\s+(?:the\s+)?(?:selected\s+)?folder)"""),

        // 简体中文
        new(
            "发票,账单|截图,屏幕截图,截屏|文档,文件文档|图片,照片,图像|压缩包,归档文件,压缩文件,档案|日志,日志文件|视频|媒体,媒体文件|游戏|程序,应用程序,应用,软件|脚本,批处理文件|服务器启动项,服务器快捷方式|所有文件,全部文件,文件",
            "照片,图片,图像|文档|财务,发票|合同,法律|归档,压缩包,档案|音乐,音频|视频|应用,程序,软件|游戏|服务器|日志|混合,其他",
            "和|与|及",
            """(?:桌面(?:上|的|上的)?)?\s*""",
            """请\s*""",
            "(?:并且|然后|但是|同时|而)(?!且)",
            """(?:(?:(?:把|将)?\s*{subjects}\s*(?:按|按照)\s*(?:(?<year>年份|年|年度)|(?<type>文件类型|类型|类别|种类)|(?<topic>主题|话题|内容))\s*(?:整理|排序|分类|分组|归类)|(?:按|按照)\s*(?:(?<year>年份|年|年度)|(?<type>文件类型|类型|类别|种类)|(?<topic>主题|话题|内容))\s*(?:整理|排序|分类|分组|归类)\s*{subjects}|(?:把|将)?\s*{subjects}\s*(?:移到|移动到|放到|放进|移入|移动至|放入)\s*(?:(?<separate>单独的文件夹)|(?:文件夹\s*)?{folder})|(?:整理|排序|分类|分组|归类)\s*{subjects}\s*(?:按|按照)\s*(?:(?<year>年份|年|年度)|(?<type>文件类型|类型|种类|类别)|(?<topic>主题|话题|内容)))|(?:(?:整理|分类|归类)\s*{subjects}|(?:把|将)\s*{subjects}\s*(?:整理|分类|归类))(?<type>))""",
            """(?:(?:把|将)?\s*{subjects}\s*(?:保留在桌面|留在桌面|不移动|不要移动|不动|保持原样)|(?:保留|别动|不要移动)\s*{subjects}(?:在桌面)?)""",
            """(?:更改|修改|更换|替换|选择|设置|自定义)\s*(?:(?:桌面(?:上|的)?)?\s*文件夹\s*)?图标(?:在桌面上)?""",
            """(?:(?:把|将)?\s*文件夹(?:的)?图标\s*(?:改成|改为|换成|更改为|设置成|设置为)\s*{icon}(?:图标)?|(?:用|使用)\s*{icon}(?:图标)?\s*(?:作为|替换)\s*文件夹(?:的)?图标)"""),

        // 日本語
        new(
            "請求書,請求書類,インボイス|スクリーンショット,画面キャプチャ,スクショ|文書,書類,ドキュメント|画像,写真|アーカイブ,圧縮ファイル,ZIPファイル|ログ,ログファイル|動画,ビデオ|メディア,メディアファイル|ゲーム|アプリ,アプリケーション,プログラム|スクリプト,バッチファイル|サーバー起動項目,サーバーのショートカット|すべてのファイル,ファイル",
            "写真,画像|書類,文書,ドキュメント|財務,請求書|契約書,法律|アーカイブ,圧縮,ZIP|音楽,オーディオ|動画,ビデオ|アプリ,アプリケーション,プログラム|ゲーム|サーバー|ログ,ジャーナル|その他,混合",
            "と|や",
            """(?:デスクトップ(?:上)?の)?\s*""",
            """(?:お願い(?:します)?|どうか)[\s、,]*""",
            "(?:そして|ただし|その一方で)",
            """(?:(?:{subjects}\s*(?:を|は)\s*(?:(?<year>年別に|年ごとに|年度別に)|(?<type>種類別に|種類ごとに|タイプ別に|ファイルの種類別に)|(?<topic>テーマ別に|テーマごとに|主題別に|内容別に))\s*(?:整理|分類|並べ替え|グループ化|分け)(?:して(?:ください)?|する|してください)?|{subjects}\s*(?:を|は)\s*(?:(?<separate>別のフォルダー?|専用フォルダー?)|(?:フォルダー?\s*)?{folder})\s*(?:に|へ)\s*(?:移動|保存|入れ|まとめ)(?:して(?:ください)?|する|て(?:ください)?)?)|{subjects}\s*(?:を|は)\s*(?:整理|分類)(?:して(?:ください)?|する)?(?<type>))""",
            """{subjects}\s*(?:は|を)\s*(?:(?:デスクトップに|そのまま|元の場所に)\s*(?:残す|残して(?:ください)?|置いて(?:ください)?)|移動しない(?:でください)?|そのままにして(?:ください)?)""",
            """(?:デスクトップ(?:上)?の)?(?:フォルダー?の?)?アイコン\s*(?:を|は)\s*(?:変更|交換|選択|設定)(?:して(?:ください)?|する)?""",
            """(?:選択した)?フォルダー?の?アイコン\s*を\s*{icon}(?:のアイコン)?\s*に\s*(?:変更|交換|設定)(?:して(?:ください)?|する)?"""),

        // Deutsch
        new(
            "rechnungen,rechnung|screenshots,screenshot,bildschirmfotos,bildschirmaufnahmen|dokumente,dokument|bilder,bild,fotos,foto|archive,archiv,zip-dateien,komprimierte dateien|protokolle,protokoll,logs,logdateien|videos,video|medien,mediendateien|spiele,spiel|programme,apps,anwendungen|skripte,skript,batchdateien|serverstarts,serververknüpfungen|alle dateien,dateien",
            "fotos,foto,bilder,bild|dokumente,dokument|finanzen,rechnungen|verträge,recht|archiv,archive,zip|musik,audio|video,videos|programme,apps,anwendungen|spiele,spiel|server|protokoll,protokolle,logs|gemischt,sonstiges",
            "und",
            """(?:(?:die|das|den|meine)\s+)?""",
            "bitte[ ,]+",
            """\s+(?:und|aber|dann|während)\s+(?!(?:(?:die|das|den)\s+)?{subject-start}(?:\s|$))""",
            """(?:(?:(?:sortier(?:e|en)?|ordn(?:e|en)|gruppier(?:e|en)?|organisier(?:e|en)?)\s+{subjects}\s+nach\s+(?:(?<year>jahr|jahren|jahrgang)|(?<type>typ|typen|dateityp|dateitypen|art|kategorie)|(?<topic>thema|themen|inhalt))|(?:verschieb(?:e|en)?|leg(?:e|en)?|sortier(?:e|en)?)\s+{subjects}\s+(?:in\s+|nach\s+)(?:(?<separate>(?:einen\s+)?separaten\s+ordner)|(?:(?:den|einen)\s+)?(?:ordner\s+)?{folder}))|(?:sortier(?:e|en)?|ordn(?:e|en)|gruppier(?:e|en)?|organisier(?:e|en)?)\s+{subjects}(?<type>))""",
            """(?:lass(?:e|en)?|behalt(?:e|en)?|verschiebe nicht)\s+{subjects}(?:\s+(?:auf dem desktop|auf dem schreibtisch|unverändert|liegen|wo sie sind))?""",
            """(?:änder(?:e|n)|tausch(?:e|en)|ersetz(?:e|en)|wähl(?:e|en)|wechsel(?:e|n))\s+(?:(?:das|die)\s+)?(?:desktop-?)?(?:ordnersymbole?|ordner-icons?|symbole?\s+(?:der|für)\s+ordner)(?:\s+auf\s+dem\s+(?:desktop|schreibtisch))?(?:\s+aus)?""",
            """(?:änder(?:e|n)|setz(?:e|en)|stell(?:e|en)|wechsel(?:e|n))\s+(?:das\s+)?(?:ordnersymbol|ordner-icon|symbol des ordners)\s+(?:zu|auf|in)\s+{icon}(?:\s+(?:um|ein))?"""),

        // Українська
        new(
            "рахунки,рахунків,рахунки на оплату,інвойси|знімки екрана,знімки екрану,скриншоти,скріншоти,скриншот|документи,документів|зображення,фото,фотографії|архіви,архівів,архів|логи,журнали|відео|медіа,медіафайли|ігри|програми,застосунки,додатки|скрипти,сценарії,пакетні файли|запуски серверів,ярлики серверів|усі файли,всі файли,файли",
            "фото,фотографії,зображення|документи,документ|фінанси,рахунки|договори,договори й право,право|архів,архіви,zip|музика,аудіо,музику|відео|програми,застосунки,додатки|ігри,гра|сервер,сервери|логи,журнал,журнали|різні файли,змішані",
            "і|й|та",
            """(?:мої\s+)?""",
            """будь\s+ласка[ ,]+""",
            """\s+(?:а|але|потім)\s+|\s+(?:і|й|та)\s+(?!(?:{subject-start})(?:\s|$))""",
            """(?:(?:(?:розклади|розсортуй|відсортуй|посортуй|сортуй|згрупуй|упорядкуй|розподіли|організуй)\s+{subjects}\s+(?:за|по)\s+(?:(?<year>роками|роком|роках)|(?<type>типами|типом|категоріями|видами)|(?<topic>темами|темою|змістом))|(?:перенеси|перемісти|склади|поклади|розклади|помісти)\s+{subjects}\s+(?:в|у|до)\s+(?:(?<separate>окрему папку)|(?:папку\s+)?{folder}))|(?:розклади|розсортуй|відсортуй|посортуй|сортуй|згрупуй|упорядкуй|розподіли|організуй)\s+{subjects}(?<type>))""",
            """(?:(?:залиш|не чіпай|не переміщуй|не перенось)\s+{subjects}|{subjects}\s+(?:залиш|не чіпай|не переміщуй|не перенось))(?:\s+(?:на (?:робочому )?столі|на місці|як є))?""",
            """(?:зміни|поміняй|заміни|встанови|обери|вибери|підбери)\s+(?:значки?|іконки?|піктограм[уи])(?:\s+(?:(?:для|у)\s+)?пап(?:ки|ок))?(?:\s+на\s+робочому\s+столі)?""",
            """(?:зміни|поміняй|заміни|встанови)\s+(?:значок|іконку|піктограму)\s+(?:(?:для|у)\s+)?(?:вибраної\s+)?папки\s+на\s+{icon}"""),

        // Беларуская
        new(
            "рахункі,рахункаў,рахункі на аплату,інвойсы|здымкі экрана,скрыншоты,скрыншот|дакументы,дакументаў|выявы,фота,фатаграфіі|архівы,архіваў,архіў|логі,журналы|відэа|медыя,медыяфайлы|гульні|праграмы,прыкладанні,дадаткі|скрыпты,сцэнарыі,пакетныя файлы|запускі сервераў,цэтлікі сервераў|усе файлы,файлы",
            "фота,выявы,фатаграфіі|дакументы,дакумент|фінансы,рахункі|дамовы,дамовы і права,права|архіў,архівы,zip|музыка,аўдыя,музыку|відэа|праграмы,прыкладанні|гульні,гульня|сервер,серверы|часопіс,логі,журнал|розныя файлы,змешаныя",
            "і|ды",
            """(?:мае\s+)?""",
            """калі\s+ласка[ ,]+""",
            """\s+(?:а|але|потым)\s+|\s+(?:і|ды)\s+(?!(?:{subject-start})(?:\s|$))""",
            """(?:(?:(?:раскладзі|адсартуй|рассартуй|сартуй|згрупуй|зарадкуй|упарадкуй|размяркуй|арганізуй)\s+{subjects}\s+(?:па|за)\s+(?:(?<year>гадах|годзе|гадамі)|(?<type>тыпах|тыпе|катэгорыях|відах)|(?<topic>тэмах|тэме|змесце))|(?:перанясі|перамясці|пакладзі|складзі|размясці|раскладзі)\s+{subjects}\s+(?:у|ў|да)\s+(?:(?<separate>асобную папку)|(?:папку\s+)?{folder}))|(?:раскладзі|адсартуй|рассартуй|сартуй|згрупуй|зарадкуй|упарадкуй|размяркуй|арганізуй)\s+{subjects}(?<type>))""",
            """(?:(?:пакінь|не чапай|не перамяшчай|не перанось)\s+{subjects}|{subjects}\s+(?:пакінь|не чапай|не перамяшчай|не перанось))(?:\s+(?:на (?:працоўным )?стале|на месцы|як ёсць))?""",
            """(?:змяні|памяняй|замяні|усталюй|устанаві|выберы|падбяры)\s+(?:значкі|значок|іконку|іконкі)(?:\s+(?:(?:для|у|ў)\s+)?пап(?:кі|ак))?(?:\s+на\s+працоўным\s+стале)?""",
            """(?:змяні|памяняй|замяні|усталюй|устанаві)\s+(?:значок|іконку)\s+(?:(?:для|у|ў)\s+)?(?:выбранай\s+)?папкі\s+на\s+{icon}"""),

        // Español
        new(
            "facturas,factura|capturas de pantalla,captura de pantalla,pantallazos|documentos,documento|imágenes,imagen,fotos,fotografías|archivos comprimidos,comprimidos,archivos zip,paquetes zip|registros,logs,archivos de registro|vídeos,videos,vídeo,video|medios,archivos multimedia,multimedia|juegos,juego|programas,aplicaciones,apps|scripts,archivos por lotes|inicios de servidor,accesos directos a servidores|todos los archivos,archivos,ficheros",
            "fotos,fotografías,imágenes,foto|documento,documentos|finanzas,facturas|contratos,legal,acuerdos|archivo,comprimidos,zip|música,audio|vídeos,videos,vídeo,video|programas,aplicaciones,apps|juegos,juego|servidores,servidor|registros,logs|mixto,variado",
            "y|e",
            """(?:(?:los|las|el|la|mis)\s+)?""",
            """por\s+favor[ ,]+""",
            """\s+(?:y|e|pero|luego|mientras)\s+(?!(?:(?:los|las|el|la)\s+)?{subject-start}(?:\s|$))""",
            """(?:(?:(?:orden(?:a|ar|e)|organiz(?:a|ar|e)|agrup(?:a|ar|e)|clasific(?:a|ar)|clasifique)\s+{subjects}\s+(?:por|según)\s+(?:(?<year>año|años)|(?<type>tipo|tipos|tipo de archivo|categoría|categorías)|(?<topic>tema|temas|contenido))|(?:mueve|mover|mueva|traslada|pasa|pon|coloca|coloque)\s+{subjects}\s+(?:a|en|dentro de)\s+(?:(?<separate>(?:una\s+)?carpeta (?:separada|aparte))|(?:(?:la|una)\s+)?(?:carpeta\s+)?{folder}))|(?:orden(?:a|ar|e)|organiz(?:a|ar|e)|agrup(?:a|ar|e)|clasific(?:a|ar)|clasifique)\s+{subjects}(?<type>))""",
            """(?:deja|deje|conserva|conserve|mantén|no muevas|no toques)\s+{subjects}(?:\s+(?:en el escritorio|en su sitio|donde están|tal como están|sin mover))?""",
            """(?:cambia|cambie|cambiar|reemplaza|modifica|elige|selecciona|establece)\s+(?:(?:el|los)\s+)?(?:iconos?|íconos?)(?:\s+(?:de las?|para las?)\s+carpetas?)?(?:\s+(?:del|en el)\s+escritorio)?""",
            """(?:cambia|cambie|cambiar|reemplaza|modifica|establece|pon)\s+(?:el\s+)?(?:icono|ícono)\s+(?:de|para)\s+(?:la\s+)?carpeta\s+(?:a|por|como)\s+(?:(?:un\s+)?(?:icono|ícono)\s+de\s+)?{icon}"""),

        // Português (Brasil)
        new(
            "faturas,fatura,notas fiscais|capturas de tela,captura de tela,prints,print|documentos,documento|imagens,imagem,fotos,foto|arquivos compactados,compactados,arquivos zip|logs,registros,arquivos de log|vídeos,vídeo,videos,video|mídia,mídias,arquivos de mídia|jogos,jogo|programas,aplicativos,apps|scripts,arquivos em lote|inicializações de servidores,atalhos de servidores|todos os arquivos,arquivos",
            "fotos,foto,imagens,imagem|documentos,documento|finanças,faturas|contratos,jurídico|arquivo,compactados,zip|música,áudio|vídeo,vídeos,video|aplicativos,programas,apps|jogos,jogo|servidor,servidores|registros,logs|diversos,misto,variado",
            "e",
            """(?:(?:os|as|o|a|meus|minhas)\s+)?""",
            """por\s+favor[ ,]+""",
            """\s+(?:e|mas|depois|enquanto)\s+(?!(?:(?:os|as|o|a)\s+)?{subject-start}(?:\s|$))""",
            """(?:(?:(?:organiz(?:e|ar|a)|orden(?:e|ar|a)|classific(?:ar|a)|classifique|agrup(?:e|ar|a)|separ(?:e|ar|a))\s+{subjects}\s+(?:por|pelo)\s+(?:(?<year>ano|anos)|(?<type>tipo|tipos|tipo de arquivo|categoria|categorias)|(?<topic>assunto|tema|temas|conteúdo))|(?:mov(?:a|er|e)|coloc(?:ar|a)|coloque|passe|transfira)\s+{subjects}\s+(?:para|na|em|dentro de)\s+(?:(?<separate>(?:uma\s+)?pasta (?:separada|à parte))|(?:(?:a|uma)\s+)?(?:pasta\s+)?{folder}))|(?:organiz(?:e|ar|a)|orden(?:e|ar|a)|classific(?:ar|a)|classifique|agrup(?:e|ar|a)|separ(?:e|ar|a))\s+{subjects}(?<type>))""",
            """(?:deixe|deixa|mantenha|conserve|não mova|não mexa)\s+{subjects}(?:\s+(?:na área de trabalho|no desktop|no lugar|onde estão|como estão|intactos?))?""",
            """(?:troque|trocar|altere|alterar|mude|substitua|escolha|selecione|defina)\s+(?:(?:o|os)\s+)?ícones?(?:\s+(?:das?|para as?)\s+pastas?)?(?:\s+(?:da|na)\s+(?:área de trabalho|mesa)|\s+do desktop)?""",
            """(?:troque|trocar|altere|alterar|mude|substitua|defina|use)\s+(?:o\s+)?ícone\s+(?:da|para a)\s+pasta\s+(?:por|para|como)\s+(?:(?:um\s+)?ícone\s+de\s+)?{icon}"""),
    ];
}
