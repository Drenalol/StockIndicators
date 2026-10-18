using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockIndicators.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedRulePresetExtraLanguages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "RulePresets" p SET
                    "NameIt" = v."NameIt", "NameDe" = v."NameDe", "NameFr" = v."NameFr",
                    "NamePtBr" = v."NamePtBr", "NameUk" = v."NameUk", "NameBe" = v."NameBe",
                    "DescriptionIt" = v."DescriptionIt", "DescriptionDe" = v."DescriptionDe",
                    "DescriptionFr" = v."DescriptionFr", "DescriptionPtBr" = v."DescriptionPtBr",
                    "DescriptionUk" = v."DescriptionUk", "DescriptionBe" = v."DescriptionBe"
                FROM (VALUES
                    ('rsi-overbought',
                     'RSI ipercomprato', 'RSI überkauft', 'RSI suracheté', 'RSI sobrecomprado', 'RSI перекупленість', 'RSI перакупленасць',
                     'L''RSI(14) entra nella zona di ipercomprato o ne esce', 'Der RSI(14) tritt in die überkaufte Zone ein oder verlässt sie',
                     'Le RSI(14) entre dans la zone de surachat ou en sort', 'O RSI(14) entra na zona de sobrecompra ou sai dela',
                     'RSI(14) входить у зону перекупленості або виходить з неї', 'RSI(14) уваходзіць у зону перакупленасці або выходзіць з яе'),
                    ('rsi-oversold',
                     'RSI ipervenduto', 'RSI überverkauft', 'RSI survendu', 'RSI sobrevendido', 'RSI перепроданість', 'RSI перепродадзенасць',
                     'L''RSI(14) entra nella zona di ipervenduto o ne esce', 'Der RSI(14) tritt in die überverkaufte Zone ein oder verlässt sie',
                     'Le RSI(14) entre dans la zone de survente ou en sort', 'O RSI(14) entra na zona de sobrevenda ou sai dela',
                     'RSI(14) входить у зону перепроданості або виходить з неї', 'RSI(14) уваходзіць у зону перепродадзенасці або выходзіць з яе'),
                    ('macd-bullish-cross',
                     'MACD incrocio rialzista', 'MACD bullisches Kreuz', 'MACD croisement haussier', 'MACD cruzamento altista', 'MACD: бичачий перетин', 'MACD: бічыны перасячэнне',
                     'Il MACD incrocia sopra la linea del segnale o l''istogramma diventa positivo', 'Der MACD kreuzt über der Signallinie oder das Histogramm dreht ins Plus',
                     'Le MACD croise la ligne de signal à la hausse ou l''histogramme passe en positif', 'O MACD cruza a linha de sinal por cima ou o histograma fica positivo',
                     'MACD перетинає сигнальну лінію вгору або гістограма йде в плюс', 'MACD перасякае сігнальную лінію ўгару або гістаграма ідзе ў плюс'),
                    ('macd-bearish-cross',
                     'MACD incrocio ribassista', 'MACD bärisches Kreuz', 'MACD croisement baissier', 'MACD cruzamento baixista', 'MACD: ведмежий перетин', 'MACD: мядзведжы перасячэнне',
                     'Il MACD incrocia sotto la linea del segnale o l''istogramma diventa negativo', 'Der MACD kreuzt unter der Signallinie oder das Histogramm dreht ins Minus',
                     'Le MACD croise la ligne de signal à la baisse ou l''histogramme passe en négatif', 'O MACD cruza a linha de sinal por baixo ou o histograma fica negativo',
                     'MACD перетинає сигнальну лінію вниз або гістограма йде в мінус', 'MACD перасякае сігнальную лінію ўніз або гістаграма ідзе ў мінус'),
                    ('bb-upper-breakout',
                     'Rottura banda superiore BB', 'Ausbruch aus dem oberen BB-Band', 'Rupture de la bande supérieure BB', 'Ruptura da banda superior BB', 'Пробій верхньої смуги BB', 'Прабой верхняй паласы BB',
                     'Il prezzo esce dalle bande verso l''alto o rientra da sopra la banda superiore', 'Der Preis verlässt die Bänder nach oben oder kehrt vom oberen Band zurück',
                     'Le prix sort des bandes vers le haut ou revient d''au-dessus de la bande supérieure', 'O preço sai das bandas para cima ou volta de cima da banda superior',
                     'Ціна виходить за смуги вгору або повертається з-за верхньої смуги', 'Цана выходзіць за палосы ўгару або вяртаецца з-за верхняй паласы'),
                    ('bb-lower-breakout',
                     'Rottura banda inferiore BB', 'Ausbruch aus dem unteren BB-Band', 'Rupture de la bande inférieure BB', 'Ruptura da banda inferior BB', 'Пробій нижньої смуги BB', 'Прабой ніжняй паласы BB',
                     'Il prezzo esce dalle bande verso il basso o rientra da sotto la banda inferiore', 'Der Preis verlässt die Bänder nach unten oder kehrt vom unteren Band zurück',
                     'Le prix sort des bandes par le bas ou revient de sous la bande inférieure', 'O preço sai das bandas para baixo ou volta de baixo da banda inferior',
                     'Ціна виходить за смуги вниз або повертається з-за нижньої смуги', 'Цана выходзіць за палосы ўніз або вяртаецца з-за ніжняй паласы'),
                    ('volume-spike-2x',
                     'Picco di volume x2', 'Volumenausstoß x2', 'Pic de volume x2', 'Pico de volume x2', 'Сплеск обсягу x2', 'Усплёск аб''ёму x2',
                     'Il volume dell''ultima candela è almeno 2 volte la media di 20 candele', 'Das Volumen der letzten Kerze ist mindestens doppelt so hoch wie der Durchschnitt von 20 Kerzen',
                     'Le volume de la dernière bougie est au moins 2 fois la moyenne de 20 bougies', 'O volume da última vela é pelo menos o dobro da média de 20 velas',
                     'Обсяг останньої свічки щонайменше вдвічі вищий за середнє за 20 свічок', 'Аб''ём апошняй свечкі як найменш удвая вышэй за сярэдняе за 20 свечак'),
                    ('gap-up',
                     'Gap rialzista 2%', 'Aufwärts-Gap 2%', 'Gap haussier 2%', 'Gap de alta 2%', 'Геп угору 2%', 'Гэп угару 2%',
                     'L''apertura è almeno il 2% sopra la chiusura precedente', 'Die Eröffnung liegt mindestens 2 % über dem Vortagsschluss',
                     'L''ouverture est d''au moins 2 % au-dessus de la clôture précédente', 'A abertura fica pelo menos 2% acima do fechamento anterior',
                     'Відкриття щонайменше на 2% вище за попереднє закриття', 'Адкрыццё як найменш на 2% вышэй за папярэдняе закрыццё'),
                    ('gap-down',
                     'Gap ribassista 2%', 'Abwärts-Gap 2%', 'Gap baissier 2%', 'Gap de queda 2%', 'Геп вниз 2%', 'Гэп ўніз 2%',
                     'L''apertura è almeno il 2% sotto la chiusura precedente', 'Die Eröffnung liegt mindestens 2 % unter dem Vortagsschluss',
                     'L''ouverture est d''au moins 2 % en dessous de la clôture précédente', 'A abertura fica pelo menos 2% abaixo do fechamento anterior',
                     'Відкриття щонайменше на 2% нижче за попереднє закриття', 'Адкрыццё як найменш на 2% ніжэй за папярэдняе закрыццё'),
                    ('rsi-oversold-volume-spike',
                     'RSI ipervenduto + volume x2', 'RSI überverkauft + Volumen x2', 'RSI survendu + volume x2', 'RSI sobrevendido + volume x2', 'RSI перепроданість + обсяг x2', 'RSI перепродадзенасць + аб''ём x2',
                     'Minimo di momentum insieme a un picco di volume (combo premium)', 'Boden des Moments zusammen mit einem Volumenschub (Premium-Kombi)',
                     'Creux de momentum accompagné d''un pic de volume (combo premium)', 'Fundo de momentum junto com um pico de volume (combo premium)',
                     'Дно імпульсу разом зі сплеском обсягу (прэміум-зв''язка)', 'Дно імпульсу разам з усплёскам аб''ёму (прэміум-звязка)'),
                    ('openchange-down-3',
                     'Giù 3% dall''apertura', '3 % unter Eröffnung', 'Baisse de 3 % depuis l''ouverture', 'Queda de 3% da abertura', 'Падіння 3% від відкриття', 'Падзенне 3% ад адкрыцця',
                     'La chiusura è almeno il 3% sotto il prezzo di apertura del giorno', 'Der Schluss liegt mindestens 3 % unter dem Eröffnungskurs des Tages',
                     'La clôture est d''au moins 3 % en dessous du prix d''ouverture du jour', 'O fechamento fica pelo menos 3% abaixo do preço de abertura do dia',
                     'Закриття щонайменше на 3% нижче за ціну відкриття дня', 'Закрыццё як найменш на 3% ніжэй за цану адкрыцця дня'),
                    ('openchange-up-3',
                     'Su 3% dall''apertura', '3 % über Eröffnung', 'Hausse de 3 % depuis l''ouverture', 'Alta de 3% da abertura', 'Зростання 3% від відкриття', 'Рост 3% ад адкрыцця',
                     'La chiusura è almeno il 3% sopra il prezzo di apertura del giorno', 'Der Schluss liegt mindestens 3 % über dem Eröffnungskurs des Tages',
                     'La clôture est d''au moins 3 % au-dessus du prix d''ouverture du jour', 'O fechamento fica pelo menos 3% acima do preço de abertura do dia',
                     'Закриття щонайменше на 3% вище за ціну відкриття дня', 'Закрыццё як найменш на 3% вышэй за цану адкрыцця дня'),
                    ('week52-new-high',
                     'Nuovo massimo di 52 settimane', 'Neues 52-Wochen-Hoch', 'Nouveau plus haut sur 52 semaines', 'Nova máxima de 52 semanas', 'Новий 52-тижневий максимум', 'Новы 52-тыднёвы максімум',
                     'La chiusura sale sopra il massimo più alto delle 52 settimane precedenti', 'Der Schluss steigt über das höchste Hoch der vorherigen 52 Wochen',
                     'La clôture dépasse le plus haut des 52 dernières semaines', 'O fechamento sobe acima da máxima mais alta das 52 semanas anteriores',
                     'Закриття піднімається вище за найвищий максимум попередніх 52 тижнів', 'Закрыццё падымаецца вышэй за найвышэйшы максімум папярэдніх 52 тыдняў'),
                    ('week52-new-low',
                     'Nuovo minimo di 52 settimane', 'Neues 52-Wochen-Tief', 'Nouveau plus bas sur 52 semaines', 'Nova mínima de 52 semanas', 'Новий 52-тижневий мінімум', 'Новы 52-тыднёвы мінімум',
                     'La chiusura scende sotto il minimo più basso delle 52 settimane precedenti', 'Der Schluss fällt unter das tiefste Tief der vorherigen 52 Wochen',
                     'La clôture passe sous le plus bas des 52 dernières semaines', 'O fechamento cai abaixo da mínima mais baixa das 52 semanas anteriores',
                     'Закриття опускається нижче за найнижчий мінімум попередніх 52 тижнів', 'Закрыццё апускаецца ніжэй за найніжэйшы мінімум папярэдніх 52 тыдняў'),
                    ('trailing-stop-long-5',
                     'Trailing stop long -5%', 'Trailing-Stop Long -5%', 'Trailing stop long -5%', 'Trailing stop de compra -5%', 'Трейлінг-стоп лонга -5%', 'Трэйл-стоп лонга -5%',
                     'Avviso quando la chiusura scende del 5% sotto il massimo più alto da quando l''avviso è stato attivato', 'Alert, wenn der Schluss um 5 % unter das höchste Hoch seit dem Scharfschalten der Alert fällt',
                     'Alerte quand la clôture baisse de 5 % sous le plus haut depuis l''armement de l''alerte', 'Alerta quando o fechamento cai 5% abaixo da máxima mais alta desde que a alerta foi armada',
                     'Алерт, коли закриття падає на 5% нижче найвищого максимуму з моменту взведення алерту', 'Апавяшчэнне, калі закрыццё падае на 5% ніжэй за найвышэйшы максімум з моманту ўзвядзення апавяшчэння')
                ) AS v("Key", "NameIt", "NameDe", "NameFr", "NamePtBr", "NameUk", "NameBe",
                       "DescriptionIt", "DescriptionDe", "DescriptionFr", "DescriptionPtBr", "DescriptionUk", "DescriptionBe")
                WHERE p."Key" = v."Key";
                
                """);

            migrationBuilder.Sql(
                """
                UPDATE "RulePresets" p SET
                    "FamilyLabelIt" = v."FamilyLabelIt", "FamilyLabelDe" = v."FamilyLabelDe",
                    "FamilyLabelFr" = v."FamilyLabelFr", "FamilyLabelPtBr" = v."FamilyLabelPtBr",
                    "FamilyLabelUk" = v."FamilyLabelUk", "FamilyLabelBe" = v."FamilyLabelBe",
                    "SegmentIt" = v."SegmentIt", "SegmentDe" = v."SegmentDe",
                    "SegmentFr" = v."SegmentFr", "SegmentPtBr" = v."SegmentPtBr",
                    "SegmentUk" = v."SegmentUk", "SegmentBe" = v."SegmentBe"
                FROM (VALUES
                    ('rsi-overbought', 'RSI', 'RSI', 'RSI', 'RSI', 'RSI', 'RSI',
                     'Ipercomprato', 'Überkauft', 'Surcheté', 'Sobrecompra', 'перекупленість', 'перакупленасць'),
                    ('rsi-oversold', 'RSI', 'RSI', 'RSI', 'RSI', 'RSI', 'RSI',
                     'Ipervenduto', 'Überverkauft', 'Survente', 'Sobrevenda', 'перепроданість', 'перепродадзенасць'),
                    ('macd-bullish-cross', 'MACD', 'MACD', 'MACD', 'MACD', 'MACD', 'MACD',
                     'Rialzista', 'Bullisch', 'Haussier', 'Altista', 'бичачий', 'бічыны'),
                    ('macd-bearish-cross', 'MACD', 'MACD', 'MACD', 'MACD', 'MACD', 'MACD',
                     'Ribassista', 'Bärisch', 'Baissier', 'Baixista', 'ведмежий', 'мядзведжы'),
                    ('bb-upper-breakout', 'Bande BB', 'BB-Bänder', 'Bandes BB', 'Bandas BB', 'Смуги BB', 'Палосы BB',
                     'Superiore', 'Oberes', 'Supérieure', 'Superior', 'верхня', 'верхняя'),
                    ('bb-lower-breakout', 'Bande BB', 'BB-Bänder', 'Bandes BB', 'Bandas BB', 'Смуги BB', 'Палосы BB',
                     'Inferiore', 'Unteres', 'Inférieure', 'Inferior', 'нижня', 'ніжняя'),
                    ('gap-up', 'Gap', 'Gap', 'Gap', 'Gap', 'Геп', 'Гэп',
                     'Su', 'Aufwärts', 'En hausse', 'Alta', 'угору', 'ўгару'),
                    ('gap-down', 'Gap', 'Gap', 'Gap', 'Gap', 'Геп', 'Гэп',
                     'Giù', 'Abwärts', 'En baisse', 'Queda', 'вниз', 'ўніз'),
                    ('openchange-up-3', 'Dall''apertura', 'Eröffnung', 'Depuis l''ouverture', 'Da abertura', 'Відкриття', 'Адкрыццё',
                     '+3%', '+3%', '+3%', '+3%', '+3%', '+3%'),
                    ('openchange-down-3', 'Dall''apertura', 'Eröffnung', 'Depuis l''ouverture', 'Da abertura', 'Відкриття', 'Адкрыццё',
                     '−3%', '−3%', '−3%', '−3%', '−3%', '−3%'),
                    ('week52-new-high', '52 settimane', '52 Wochen', '52 semaines', '52 semanas', '52 тижні', '52 тыдні',
                     'Massimo', 'Hoch', 'Plus haut', 'Máxima', 'максімум', 'максімум'),
                    ('week52-new-low', '52 settimane', '52 Wochen', '52 semaines', '52 semanas', '52 тижні', '52 тыдні',
                     'Minimo', 'Tief', 'Plus bas', 'Mínima', 'мінімум', 'мінімум')
                ) AS v("Key", "FamilyLabelIt", "FamilyLabelDe", "FamilyLabelFr", "FamilyLabelPtBr", "FamilyLabelUk", "FamilyLabelBe",
                       "SegmentIt", "SegmentDe", "SegmentFr", "SegmentPtBr", "SegmentUk", "SegmentBe")
                WHERE p."Key" = v."Key";
                
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM "RulePresets"
                """);
        }
    }
}
