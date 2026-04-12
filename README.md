> [!WARNING]
> Проект архивируется вслед [YandexMusicModClient](https://github.com/TheKing-OfTime/YandexMusicModClient), Pulse Sync Client не будет поддерживатся в связи установленной на исходный код лицензией. Патчер будет продолжать работать до момента поломки клиента.

> [!NOTE]
> Возможно вас заинтересует альтернатиный клиент Яндекс Музыки [YAYMA](https://github.com/DarkPlayOff/YAYMA), однако он не предоставляет обход Плюса

# 🎵 Яндекс Музыка — мод без Плюса

Набор модов для десктопного приложения Яндекс Музыка, позволяющих пользоваться программой без подписки, скачивать треки
и многое другое

![WMJ26GQsLBTFYM5](https://github.com/user-attachments/assets/7deb631e-c67a-4d68-8a19-1e0fcd374ff1)

[![Последний релиз](https://img.shields.io/github/downloads/DarkPlayOff/YandexMusicExtMod/total?style=flat&label=%D0%A1%D0%BA%D0%B0%D1%87%D0%B0%D1%82%D1%8C)](https://github.com/DarkPlayOff/YandexMusicExtMod/releases/latest/)

## 👀 Преимущества:
🔧 Поддержка Windows/MacOS/Debian,Arch Linux

✔️ Работает без плюса

👁️ Отключена аналитика и слежка

## ✨Возможности [YandexMusicModClient](https://github.com/TheKing-OfTime/YandexMusicModClient):

### Discord Статус

<details>
   <summary>Подробнее</summary>

<details>
   <summary>Настройки</summary>

      "discordRPC": {
			"enable": true or false,                         //Включает или отключает disocrd RPC
			"applicationIDForRPC": "1124055337234858005",    //ID пользовательского приложения вашего для discord RPC
			"showButtons": true or false,                    //Включает или отключает все кнопки в статусе discord 
			"overrideDeepLinksExperiment": true or false,    //Включает или отключает разделение веб-кнопок и кнопок рабочего стола на одну кнопку
			"showGitHubButton": true or false,               //Включает или отключает кнопку Github, если для параметра overrideDeepLinksExperiment установлено значение true
			"afkTimeout": 15,				 //Время в минутах через которое статус в дискорде пропадёт если трек был поставлен на паузу.
			"showAlbum": true or false,                      //Включает или отключает строчку с информацией о альбоме в статусе discord 
   			"showSmallIcon": true or false,                  //Включает или отключает иконку статуса прослушивания в статусе discord 
      }

</details>


Добавляет поддержку отображения текущего трека как статуса в Discord
![image](https://github.com/user-attachments/assets/ff3b0726-6f83-4849-bce6-c5eb31523efa)

</details>

### Управление плеером с других устройств

<details>
   <summary>Подробнее</summary>


Добавляет поддержку управления воспроизведением настольного клиента с других устройств.

<img width="250" alt="Список устройств для воспроизведения" src="https://github.com/user-attachments/assets/17196b75-85c4-42f0-af81-ab62123fde5c">
<img width="250" alt="Управление воспроизведение с телефона на ПК клиенте" src="https://github.com/user-attachments/assets/305a94f9-4908-4c47-9d75-c0838dbad805">

<details>
   <summary>Настройки</summary>

Можно выключить в настройках внутри приложения

![image](https://github.com/user-attachments/assets/8b7280d6-f2ef-4a0e-8835-32e173a1e843)

</details>

</details>

### Скробблинг Last.FM

<details>
   <summary>Подробнее</summary>


Добавляет поддержку cкробблинга в Last.FM. Трек заскробблится если вы прослушаете хотя бы его половину. (Но при этом
запрос скроббла отправиться при смене трека)

<img width="550" alt="Страница пользователя Last.FM с заскроббленными треками" src="https://github.com/user-attachments/assets/9a47a37b-b895-4a06-8538-fb94eb009290">

<details>
   <summary>Настройки</summary>

Авторизоваться в Last.FM, а также включить/выключить функцию можно в соответствующем меню в настройках приложения.

![Яндекс_Музыка_YCvwJPSvMt](https://github.com/user-attachments/assets/76c25ff0-fddd-4747-93ba-a6ab60efe876)

<details>
   <summary>Процесс авторизации</summary>

https://github.com/user-attachments/assets/079f8b38-ca6b-4fef-b6a2-efa853fd583f

</details>

</details>

</details>

### Глобальные хоткеи

<details>
   <summary>Подробнее</summary>


Добавляет поддержку глобальных хоткеев.

<details>
   <summary>Настройки</summary>

	"globalShortcuts": {
		"TOGGLE_PLAY": "Ctrl+/",
		"MOVE_FORWARD": "Ctrl+,",
		"MOVE_BACKWARD": "Ctrl+.",
		"TOGGLE_SHUFFLE": "Ctrl+\'",
		"REPEAT_NONE": undefined,
		"REPEAT_CONTEXT": undefined,
		"REPEAT_NONE": undefined,
  		"TOGGLE_LIKE": undefined,
  		"TOGGLE_DISLIKE": undefined,
	}

</details>

</details>

### Кнопки в превью панели задач

<details>
   <summary>Подробнее</summary>


Добавляет поддержку расширений панели задач (Taskbar Extensions)

<details>
   <summary>Настройки</summary>

      "taskBarExtensions": {
			"enable": true or false //Включает или отключает расширения панели задач
		}

</details>

![image](https://github.com/TheKing-OfTime/YandexMusicModClient/assets/68960526/8c3711a3-4bb7-4601-a291-b5c7eb5f58f0)

</details>

### Возврат кнопки дизлайка

<details>
   <summary>Подробнее</summary>

Возвращает кнопку дизлайка в плеер на главной.

![image](https://github.com/user-attachments/assets/22a83331-dfc4-4c7b-92c9-4fdbe2758910)

</details>

### Возврат кнопки повтора

<details>
   <summary>Подробнее</summary>

Возвращает кнопку повтора в плеер на главной когда играет Моя Волна.

</details>

### Отображение качества трека

<details>
   <summary>Подробнее</summary>

Отображает качество либо кодек текущего трека

<details>
   <summary>Настройки</summary>

	"playerBarEnhancement": {
  		"showDislikeButton": true //Включает или выключает отображение кнопки дизлайка в проигрывателе.
		"showCodecInsteadOfQualityMark": true //Показать кодек вместо качества
	}

</details>

![image](https://github.com/user-attachments/assets/da143017-b9ff-4faf-91dc-b9ccc81b1e2f)
![image](https://github.com/user-attachments/assets/3e5b6fb2-fbd3-4e04-880c-f1e556d8c4ef)

</details>

### Улучшенная анимация Моей Волны

<details>
   <summary>Подробнее</summary>

Улучшает поведение анимации Моей Волны. Она начинает лучше адаптироваться к музыке. Также позволяет настраивать частоту
кадров в секунду при рендеринге анимации.
<details>
   <summary>Настройки</summary>

      "vibeAnimationEnhancement": {
	    "maxFPS": 25,             	// Максимально допустимая частота кадров в секунду. По умолчанию: 25. Рекомендуемое: 25 - 144. Не устанавливайте значание меньше 1
	    "intensityCoefficient": 1, 	// Чувствительность музыкального анализа. По умолчанию: 1; Рекомендуемое: 0,5 - 2; При значении 0 отключается улучшение анимации (почти :D)
	    "linearDeBoost": 5,		// [УСТАРЕЛО] Коэффициент выделения пиков в треке от основного трека. По умолчанию: 5. Рекомендуемое: 2 - 8. Если 1, отключает разделение пиков.
	    "playOnAnyEntity": false,	// Если включено, анимация воспроизводится, даже если источник трека не Моя Волна.
	    "disableRendering": false	// Полностью отключает анимацию. Используйте только если почувствуете значительное падение кадров в секунду. В противном случае подберите оптимальное значение параметра maxFPS для вашей системы.
      }

</details>

До:

https://github.com/user-attachments/assets/23a8da4d-3d6a-43c6-a5f5-965e065ed912

После:

https://github.com/user-attachments/assets/b062a3ee-d05e-4cf3-8e03-b6f8bf66525c

</details>

### Поиск при добавлении трека в плейлист

<details>
   <summary>Подробнее</summary>

Добавляет строку поиска в контекстное меню выбора плейлиста.

![image](https://github.com/user-attachments/assets/03924f52-6e37-4d6a-ad9e-c079ec739cd8)


</details>

### Информация о скачанных треках

<details>
   <summary>Подробнее</summary>

Добавляет информацию о скачанных треках на страницу настроек (количество скачанных треков и используемое хранилище для
скачанных треков)

![image](https://github.com/user-attachments/assets/d3ba9ada-941c-4bd2-8c53-dad54090bf4e)


</details>

### Скачивание текущего трека в файл

<details>
   <summary>Подробнее</summary>

Позволяет скачать текущий трек вам на ПК. Нажмите на иконку качества/кодека трека чтобы выбрать путь для размещения
файла.

</details>

### Эксперименты

<details>
   <summary>Подробнее</summary>

Позволяет включать/выключать эксперементы. Для этого вам нужно включить enableDevTools и использовать UI в приложении в
dev панели:

</details>

### Devtools & Панель Разработчика

<details>
   <summary>Подробнее</summary>

Devtools по умолчанию отключены. Чтобы включить их, вам необходимо изменить `%appdata%\YandexMusic\config.json`:

Измените `"enableDevTools": false` на `"enableDevTools": true`

![electron_L6SeZLnSAH](https://github.com/TheKing-OfTime/YandexMusicModClient/assets/68960526/ae841087-d910-45e5-a007-3fd869a493e1)

![electron_y6aOeckPLH](https://github.com/TheKing-OfTime/YandexMusicModClient/assets/68960526/4bde4785-9196-4ac6-ad3b-9ac5db5b61c8)

</details>

# 💻 Как установить и пользоваться:

 <details>
 <summary>Windows</summary>

1. [Скачайте последнюю версию](https://github.com/DarkPlayOff/YandexMusicExtMod/releases/latest)
2. После запустите `YandexMusicPatcher-win`
3. Нажмите на кнопку **«Установить мод»**
4. На рабочем столе появится ярлык для запуска

   </details>

<details>
<summary>MacOS</summary>

1. Отключите [SIP](https://developer.apple.com/documentation/security/disabling-and-enabling-system-integrity-protection)
2. [Скачайте последнюю версию](https://github.com/DarkPlayOff/YandexMusicExtMod/releases/latest)
3. Распакуйте 7zip архив
4. Запустите `YandexMusicPatcher-mac`
5. Нажмите кнопку **«Установить мод»**
6. Приложение появится в папке с программами

</details>

 <details>
 <summary>Linux</summary>

1. [Скачайте последнюю версию](https://github.com/DarkPlayOff/YandexMusicExtMod/releases/latest)
2. После запустите `YandexMusicPatcher-linux` через терминал командой `sudo -E`
3. Нажмите на кнопку **«Установить мод»**
4. Можно запускать как обычный клиент Яндекс Музыки
   </details>

Если вы захотите обновиться на новую версию модификации, просто повторно запустите патчер и нажмите на кнопку "Обновить мод"

## ❤️Благодарности

• [Stephanzion](https://github.com/Stephanzion) за [патчер](https://github.com/Stephanzion/YandexMusicBetaMod) для активации плюса

• [TheKing-OfTime](https://github.com/TheKing-OfTime) за [модификацию](https://github.com/TheKing-OfTime/YandexMusicModClient) Яндекс Музыки

• Google Gemini
