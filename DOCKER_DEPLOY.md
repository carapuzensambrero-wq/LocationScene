# Docker-деплой Unity/FishNet сервера без сборки Unity внутри Docker

Эта инструкция описывает вариант, при котором Docker **не собирает Unity-проект**. Unity/FishNet серверный билд собирается отдельно, а Dockerfile только упаковывает уже готовую Linux-сборку в образ и запускает ее.

Проект: `LocationWorkshop`  
Unity: `2022.3.62f2`

## Общая схема

```text
Unity Editor / CI
  -> Linux server build
  -> папка Build/LinuxServer
  -> docker build
  -> Docker image
  -> запуск контейнера на сервере
```

Важно: в Docker-образ **не нужно** класть Unity Editor, `Library`, исходники проекта или Unity-сборщик. В образ копируется только готовый Linux-билд.

## 1. Собрать Linux server build в Unity

В Unity нужно собрать сервер под Linux.

Рекомендуемый путь вывода:

```text
Build/LinuxServer/
```

Ожидаемая структура после сборки примерно такая:

```text
Build/LinuxServer/
  LocationWorkshop.x86_64
  LocationWorkshop_Data/
  UnityPlayer.so
  UnityPlayer_s.debug   # может отсутствовать
```

Если имя executable отличается, дальше в Dockerfile нужно использовать фактическое имя файла.

Для серверного билда желательно использовать Dedicated Server / Headless build. Если проект запускается как обычный Linux-билд, контейнер все равно должен запускать его с аргументами:

```text
-batchmode -nographics
```

## 2. Dockerfile только для упаковки готового билда

Создать в корне проекта файл `Dockerfile`:

```dockerfile
FROM ubuntu:22.04

WORKDIR /app

# Копируем только готовый Linux build, а не весь Unity-проект.
COPY Build/LinuxServer/ /app/

# Имя файла должно совпадать с executable из Linux-билда.
RUN chmod +x /app/LocationWorkshop.x86_64

# Укажи порт, который реально использует FishNet Transport.
# Для UDP-транспорта:
EXPOSE 7777/udp

CMD ["/app/LocationWorkshop.x86_64", "-batchmode", "-nographics"]
```

Если FishNet использует другой порт или TCP, замени `7777/udp` на нужное значение, например:

```dockerfile
EXPOSE 7777/tcp
```

или добавь оба варианта, если они реально нужны:

```dockerfile
EXPOSE 7777/tcp
EXPOSE 7777/udp
```

## 3. Рекомендуемый `.dockerignore`

Чтобы Docker случайно не отправлял в build context весь Unity-проект, лучше добавить `.dockerignore`.

Вариант, при котором в контекст попадает только готовый билд и Dockerfile:

```dockerignore
*
!Dockerfile
!Build/
!Build/LinuxServer/
!Build/LinuxServer/**
```

Это важно: Unity-папки `Library`, `Temp`, `Obj`, `.git`, `Assets` и остальные исходники не нужны для упаковки готового билда.

## 4. Собрать Docker image

Из корня проекта:

```bash
docker build -t location-workshop-server:latest .
```

На Windows в `cmd.exe` команда такая же:

```cmd
docker build -t location-workshop-server:latest .
```

## 5. Запустить контейнер локально

Пример для UDP-порта `7777`:

```bash
docker run --rm -it ^
  --name location-workshop-server ^
  -p 7777:7777/udp ^
  location-workshop-server:latest
```

Для Linux shell вместо `^` используется `\`:

```bash
docker run --rm -it \
  --name location-workshop-server \
  -p 7777:7777/udp \
  location-workshop-server:latest
```

Для фонового запуска:

```bash
docker run -d \
  --name location-workshop-server \
  --restart unless-stopped \
  -p 7777:7777/udp \
  location-workshop-server:latest
```

Логи:

```bash
docker logs -f location-workshop-server
```

Остановка:

```bash
docker stop location-workshop-server
```

## 6. Что проверить в FishNet

Перед деплоем нужно убедиться, что сервер действительно стартует в headless/container-режиме.

Минимальный чек-лист:

1. В сцене есть `NetworkManager` FishNet.
2. Выбран и настроен Transport.
3. Порт Transport совпадает с портом в `EXPOSE` и `docker run -p`.
4. Сервер запускается автоматически в серверном билде, например через bootstrap-логику, которая в batch/headless режиме вызывает старт сервера.
5. Клиентская логика, UI, камера, ввод и локальный игрок не блокируют запуск выделенного сервера.

`EXPOSE` сам по себе порт наружу не публикует. Он только документирует порт образа. Реальный проброс делается через:

```bash
-p 7777:7777/udp
```

## 7. Автодеплой без Unity-сборщика в Dockerfile

Правильный pipeline для этого подхода:

```text
push в репозиторий
  -> CI собирает Unity Linux server build
  -> CI кладет результат в Build/LinuxServer
  -> docker build упаковывает Build/LinuxServer
  -> image пушится в registry
  -> сервер подтягивает новый image
  -> контейнер перезапускается
```

Dockerfile при этом остается простым и не содержит команд запуска Unity Editor.

## 8. Частые ошибки

- Скопировали в Docker только `LocationWorkshop.x86_64`, но забыли `LocationWorkshop_Data` и `UnityPlayer.so`.
- Собрали Windows build вместо Linux build.
- Не дали executable права на запуск через `chmod +x`.
- Пробросили TCP, хотя Transport работает по UDP, или наоборот.
- FishNet сервер не стартует сам, потому что запуск рассчитан только на кнопку в UI или ручной запуск из редактора.
- Dockerfile пытается собирать Unity-проект, хотя задача — только упаковать готовый билд.

## Итог

Для этого проекта Dockerfile должен выполнять только упаковку:

```text
готовый Build/LinuxServer -> Docker image -> запуск LocationWorkshop.x86_64
```

Сборка Unity-билда должна происходить отдельно: вручную в Unity Editor или отдельным CI-шагом до `docker build`.
