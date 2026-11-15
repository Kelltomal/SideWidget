"""Train SideWidget's supervised fastText model and export its CPU inference weights.

Training only: pip install fasttext-wheel==0.9.2 numpy==1.26.4 (Python 3.12).
Run from any directory: python tools/desktop-fasttext/train.py
"""

import gzip
import json
from pathlib import Path
import random
import re
from tempfile import TemporaryDirectory

import fasttext


TOPICS = {
    'finance': '''счёт на оплату итоговая сумма банковские реквизиты|поступления и списания по расчётному счёту банковская выписка|учёт бюджета доходы расходы остаток касса|налоговая декларация начисление НДС|платёжное поручение получатель банк перевод|смета стоимость работ цена итого|чек за покупку кассовый терминал возврат|отчёт о движении денежных средств баланс|кредит проценты остаток задолженности погашение|зарплатная ведомость оклад удержания|накладная количество стоимость товара НДС|invoice billing payment bank account amount due|budget expenses income monthly balance|purchase receipt refund total tax|account statement transactions opening balance|financial forecast cash flow profit margin|payroll salary gross deductions|tax return deductions taxable income''',
    'legal': '''договор оказания услуг заказчик исполнитель стороны подписали|соглашение о расторжении договора обязательства сторон|претензия нарушение условий договора неустойка|доверенность представитель полномочия срок действия|исковое заявление истец ответчик суд|юридическое заключение правовая экспертиза|договор аренды арендодатель помещение срок аренды|согласие на обработку персональных данных оператор субъект|устав общества учредитель юридическое лицо|дополнительное соглашение к контракту|политика конфиденциальности обработка данных|service agreement contractual terms signed parties|employment contract employer obligations|lease agreement tenant landlord property|nondisclosure agreement confidential information|power of attorney authorized representative|legal notice breach of contract liability|court filing claimant defendant judgment''',
    'projects': '''план проекта этапы сроки ответственные результат|спринт бэклог задачи приоритет релиз|техническое задание требования критерии приёмки|портфолио кейс проекта стек архитектура реализация|дорожная карта продукта запуск релизы|протокол встречи команды решения действия сроки|бриф дизайн макет интерфейс согласование|проектирование системы интеграция сервисов API|проект отчёт о ходе работ прогресс блокеры|реестр рисков проекта сроки зависимости|план тестирования сценарии баги сборка|project roadmap milestones deliverables team|sprint backlog release planning tickets|technical requirements acceptance criteria|project portfolio case study implementation|design brief prototype user flows|meeting minutes action items owners|deployment checklist testing integration''',
    'study': '''лекция семинар конспект курса учебная программа|домашнее задание упражнения ответы|экзамен зачёт вопросы билеты|дипломная работа научный руководитель библиография|курсовая работа студент факультет|лабораторная работа цель эксперимента выводы|учебник глава теория практические задачи|расписание занятий группа преподаватель|словарь термины изучение английского|тест по предмету оценка баллы|методические рекомендации для студентов|lecture notes class syllabus semester|homework assignment practice exercises|exam preparation study guide questions|thesis research supervisor bibliography|laboratory experiment student worksheet|course schedule classroom professor|vocabulary workbook language learning''',
    'personal': '''семейные фотографии альбом день рождения|рецепт пирога ингредиенты приготовление духовка|маршрут поездки отпуск отель авиабилеты|личный дневник воспоминания семья выходные|список покупок продукты магазин ужин|тренировка упражнения подходы повторения|бронирование гостиницы заселение выезд|приглашение на свадьбу гости праздник|ветеринар прививка питомец собака|домашние дела ремонт квартиры мебель|путешествие маршрут экскурсии багаж|family album birthday memories|recipe ingredients cooking oven|holiday itinerary hotel booking flight|grocery shopping list dinner|personal journal weekend family|workout routine sets repetitions|wedding invitation guests celebration''',
    'unknown': '''файл новый копия документ|черновик данные текст версия|заметка разное общий список|результаты выгрузка файл образец|часть один два строка|new file document copy final|untitled draft notes assorted|sample random data text|test export backup fragment|misc temporary information table''',
}


def features(text):
    tokens = []
    for word in re.findall(r'[^\W\d_]+', text.lower().replace('ё', 'е')):
        if len(word) < 2:
            continue
        tokens.append('w_' + word)
        padded = '<' + word + '>'
        tokens.extend('c_' + padded[i:i+n] for n in (3, 4) for i in range(len(padded) - n + 1))
    return ' '.join(tokens)


RUSSIAN = {
    'finance': '''платежку бухгалтер отправил в клиент банк платежное поручение|закрывающие документы закрывашки акт сверки оплатили поставщику|счет фактура входящий ндс расчеты с контрагентами|деньги поступили на расчетный счет сверили обороты|куда делись деньги отчет о тратах выручке и расходах|реестр оплат платежи за аренду и услуги|начислили премиальные сотрудникам зарплатный реестр|бухучет оборотка по счетам дебиторка кредиторка|финотчет валовая прибыль себестоимость|поступления выписка из банка возврат комиссии|кассовый разрыв остатки на счетах|сверить взаиморасчеты по накладным|отчет по зарплате аванс начисления удержания|чек оплата картой сумма покупки''',
    'legal': '''подписали допник к договору внесли изменение условий|заключили допсоглашение о продлении аренды|адвокат подал ходатайство судье по гражданскому делу|приобщить документы к делу подать возражения в суд|нотариальная доверка на совершение сделок|выдали доверенность подписанту представлять интересы|протокол разногласий к договору согласовать редакцию пункта|проверка условий сделки юридический отдел|уведомление о расторжении договора со стороны исполнителя|регламент обработки персональных данных и согласие|досудебка требование о взыскании неустойки|договор поставки ответственность и гарантии сторон|оформление полномочий представителя ип доверитель|судебное заседание апелляционная жалоба''',
    'projects': '''тз для разработки доработать интерфейс по макету|переделка админки добавить поиск согласовать дизайн|тикеты багфиксы сборка и выкатка релиза в прод|починили баг билд готов к деплою|фиксы накатить после проверки на стенде|таски с созвона разбить на спринты|ретро команды проблемы итерации и бэклог|план демо проверить прототип и собрать обратную связь|фидбек с демонстрации обсудить интеграцию|планерка команда обсудила сроки и блокеры|чеклист выкладки на сервер версия сборки|смета проекта дорожная карта и вехи|перечень правок по продукту согласование и приемка|описание архитектуры модуля и зависимостей''',
    'study': '''домашку по математике сдать преподавателю на занятии|задания для подготовки к паре задачник упражнения|шпаргалки к экзамену билеты и конспекты|повторить теорию к зачету по лекциям|курсач проверить с научным руководителем|научрук отправил правки к дипломной и списку источников|отчет по лабе измерения наблюдения и ошибки измерения|лабы по физике таблица эксперимента и погрешность|семинарка ответы на вопросы по учебнику|дз к уроку решить примеры из методички|препод перенес контрольную и консультацию|вкр аннотация введение обзор литературы|пересдача зачета учебный деканат|подготовка к сессии зачетка расписание пар''',
    'personal': '''гостинцы родителям подарки к юбилею открытка|собраться на дачу угли шампуры мясо для шашлыков|мангал на природе список вещей для пикника|котенок осмотр у ветеринара прививки ветеринарный паспорт|ветпаспорт питомца прививочный сертификат|блины рецепт молоко мука яйца и сковорода|оладьи на завтрак замесить тесто и обжарить|план отпуска тур бронь жилья билеты|семейный ужин список продуктов в магазин|поздравления друзьям подарки на праздник|список дел по дому и поездка на выходных|семейный фотоальбом снимки с выпускного|уборка квартиры и покупки для дома|маршрут прогулки и остановки на перекус''',
    'unknown': '''обсудить потом черновой вариант и дополнить|проверочная выгрузка версия без описания|уточнить во сколько встречаемся и куда ехать|план проекта учесть траты на подготовку к учебе|записка разное посмотреть вечером|заменить старый файл последней копией|черновик договора проекта для обсуждения бюджета|встреча по списку пока без темы|выгрузка за период просто сохранить|какой вариант оставить нужно уточнить''',
}

# Documents typical of each topic, including vocabulary shared with other topics.
# Keep evaluation files separate from these seed phrases.
MORE_TOPICS = {
    'health': '''анализ крови общий клинический гемоглобин лейкоциты тромбоциты|биохимическое исследование глюкоза холестерин печеночные пробы|лаборатория показатель норма референсный интервал мочевина|креатинин в сыворотке оценка функции почек|билирубин общий и прямой результат исследования|заключение рентгенолога легочные поля очаговые изменения|рентгенография органов грудной клетки направление к врачу|осмотр терапевта жалобы анамнез предварительный диагноз|схема лечения дозировка таблетки принимать во время еды|рецепт врача действующее вещество курс лечения|направление на повторную консультацию поликлиника|выписка из истории болезни стационар назначенное лечение|эпикриз госпитализация пациент лечащий врач|дневник самоконтроля гипертония измерение давления пульс|запись к кардиологу время приема кабинет|флюорография результаты обследования органов дыхания|стоматология план лечения кариес пломба|прививка от гриппа вакцинация медицинский отвод|медицинская справка заключение специалиста допуск|ultrasound radiology report clinical findings|blood chemistry results reference range laboratory|hospital discharge summary medication instructions|prescription dosage follow up appointment|blood pressure heart rate monitoring patient''',
    'travel': '''бронирование авиаперелета маршрутная квитанция пассажир рейс|посадочный билет вылет выход на посадку время регистрации|электронный авиабилет багаж тариф стыковка|аэропорт терминал отправления ручная кладь|трансфер встреча в аэропорту адрес гостиницы|подтверждение брони отеля заезд выселение номер|туристический ваучер отель размещение питание|железнодорожный билет поезд вагон место вокзал|маршрут поезда ржд прибытие станция отправления|пересадка в пути расписание поездов вокзал|шенгенская виза анкета турпоездка загранпаспорт|туристическая поездка страховка визовый центр|план маршрута перелеты ночевки экскурсии|перенос рейса перевозчик задержка вылета|бронь жилья в отпуске подтверждение заселения|круиз маршрут порты отправления каюта|поездка за границу маршрут билеты и бронь|поездка выходные экскурсионная программа|flight booking confirmation passenger itinerary airport|boarding pass check in seat terminal|hotel voucher room reservation check out|train ticket carriage railway departure station|visa application passport tourism reservation|flight connection layover baggage allowance''',
    'auto': '''техническое обслуживание автомобиля регламент пробег замена масла|заказ наряд автосервис фильтры свечи тормозная жидкость|тормозные диски колодки замена на сто|масляный воздушный салонный фильтры комплект то|шинный сервис сезонная переобувка балансировка шин|шиномонтаж давление в колесах размер резины|полис осаго каско страхователь водитель автомобиль|осаго государственный регистрационный знак вин|вписать водителя в страховку машины страховой полис|ошибка двигателя сканер диагностика коробки передач|ремонт подвески замена амортизатора запчасти|свидетельство о регистрации транспортного средства стс|паспорт транспортного средства птс кузов вин|гибдд постановка на учет госномера машина|автозапчасти артикулы деталей сервисная книжка|проверка уровня масла антифриза и износ шин|сход развал передних колес диагностика ходовой|пробег одометр регламент ремонта|vehicle registration vin license plate|car service maintenance mileage oil filter|vehicle insurance policy driver registration|engine diagnostic trouble codes transmission|brake pads tire rotation wheel alignment|automotive parts service order''',
}

TRAVEL_SEEDS = {
    'маршрут поездки отпуск отель авиабилеты',
    'бронирование гостиницы заселение выезд',
    'путешествие маршрут экскурсии багаж',
    'holiday itinerary hotel booking flight',
    'план отпуска тур бронь жилья билеты',
}


def main():
    rng = random.Random(81)
    rows = []
    phrases_by_label = {label: (phrases + '|' + RUSSIAN[label]).split('|') for label, phrases in TOPICS.items()}
    phrases_by_label.update({label: phrases.split('|') for label, phrases in MORE_TOPICS.items()})
    phrases_by_label['travel'].extend(sorted(TRAVEL_SEEDS))
    phrases_by_label['personal'] = [phrase for phrase in phrases_by_label['personal'] if phrase not in TRAVEL_SEEDS]
    phrases_by_label['unknown'].extend([
        'план денег на поездку и здоровье учеба и машина',
        'заметки обо всем договор бюджет учеба медицинская справка',
        'обсудить раньше чем отправить список без названия',
    ])
    for label, phrases in phrases_by_label.items():
        for phrase in phrases:
            words = phrase.split()
            examples = [phrase, ' '.join(words[:3]), ' '.join(words[-3:])]
            examples += [' '.join(rng.sample(words, rng.randint(2, len(words)))) for _ in range(5)]
            rows.extend(f'__label__{label} {features(example)}' for example in examples)
    rng.shuffle(rows)
    export(rows, 'desktop-fasttext', ('расходы банковская выписка', 'lease tenant agreement', 'копия документ', 'спринт проект'))


def export(rows, name, samples):
    with TemporaryDirectory() as directory:
        train = Path(directory) / 'train.txt'
        train.write_text('\n'.join(rows) + '\n', encoding='utf-8', newline='\n')
        model = fasttext.train_supervised(str(train), dim=16, epoch=65, lr=0.45,
                                         loss='softmax', bucket=0, thread=1, verbose=0)
        # Mean input embedding followed by a linear output layer. Fold both
        # matrices into token-to-logit weights for the managed C# runtime.
        weights = model.get_input_matrix() @ model.get_output_matrix().T
        labels = [label.removeprefix('__label__') for label in model.get_labels()]
        data = dict(labels=labels, weights={word: [round(float(v), 5) for v in weights[i]]
                                           for i, word in enumerate(model.get_words())})
        for sample in samples:
            row = features(sample)
            tokens = [word for word in row.split() + ['</s>'] if word in data['weights']]
            logits = [sum(data['weights'][word][i] for word in tokens) / len(tokens) for i in range(len(labels))]
            predicted, _ = model.predict(row)
            assert labels[max(range(len(labels)), key=logits.__getitem__)] == predicted[0].removeprefix('__label__')
    asset = Path(__file__).resolve().parents[2] / f'src/SideWidget.Infrastructure/Assets/{name}.json.gz'
    asset.parent.mkdir(exist_ok=True)
    asset.write_bytes(gzip.compress(json.dumps(data, ensure_ascii=False, separators=(',', ':')).encode(), mtime=0))
    print(f'{len(rows)} training rows, {len(data["weights"])} tokens, {asset.stat().st_size} bytes')


if __name__ == '__main__':
    main()
