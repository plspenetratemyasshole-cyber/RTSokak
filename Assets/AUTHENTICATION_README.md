# Система авторизации RTSokak через Kaplao Games

## 📋 Обзор

Система авторизации позволяет игрокам входить через **тот же веб-сайт что и KaplaoGames** (localhost:3000), получать уникальный nickname и переподключаться к игре с сохранением своих юнитов.

### Преимущества над IP-based системой:
✅ Работает через NAT и роутеры  
✅ Не зависит от смены IP  
✅ Один аккаунт = один игрок (даже если IP изменился)  
✅ Используется тот же сайт что и KaplaoGames  

---

## 🔧 Созданные файлы

### 1. **Assets/Scripts/Shared/RTSPlayerSession.cs**
Хранилище данных игрока (nickname, email, token) в `PlayerPrefs`.

**Поля:**
- `Nickname` - ник игрока из аккаунта
- `AccountEmail` - email для дополнительной проверки
- `AuthToken` - токен авторизации
- `IsLoggedIn` - авторизован ли игрок

**Методы:**
- `Load()` - загрузить из PlayerPrefs
- `SetAccount(nickname, email, token)` - сохранить данные аккаунта
- `SignOutKeepLocalNickname()` - выйти из аккаунта

---

### 2. **Assets/Scripts/Shared/RTSAccountConfig.cs**
Конфигурация URL веб-сайта для авторизации.

**Поля:**
- `websiteUrl` - базовый URL сайта (по умолчанию `http://localhost:3000`)

**Где добавить:** На объект **Main Menu** в сцене MainMenu.

---

### 3. **Assets/Scripts/Shared/RTSAccountClient.cs**
Device-code авторизация через веб-сайт (как в KaplaoGames).

**Процесс авторизации:**
1. Игрок нажимает "Sign in"
2. Запрос `/api/device/start` → получает `deviceCode`
3. Открывается браузер: `website.com/device-signin?device=CODE`
4. Игрок входит на сайте
5. Клиент poll'ит `/api/device/poll?code=CODE`
6. Получает `token`, `nickname`, `email` → сохраняет в `RTSPlayerSession`

**События:**
- `SignedIn` - успешная авторизация
- `SignInFailed` - ошибка авторизации
- `SignedOut` - выход из аккаунта
- `StatusChanged` - изменение статуса (для UI)

**Где добавить:** На объект **Main Menu** в сцене MainMenu.

---

## 🎮 Как это работает

### **1. Запуск игры**
```
MainMenuUI.Awake()
  → RTSPlayerSession.Load()  // Загрузить nickname из PlayerPrefs
```

### **2. Авторизация (если нужна)**
```
UI кнопка "Sign In"
  → RTSAccountClient.BeginSignIn()
  → Открывается браузер с device-signin
  → Игрок входит на сайте
  → RTSAccountClient получает nickname + token
  → RTSPlayerSession.SetAccount(nickname, email, token)
```

### **3. Подключение к серверу**
```
Клиент подключается
  → RTSPlayer.Start() (isLocalPlayer)
  → CmdRegisterClient(nickname, email) отправляется серверу
```

### **4. Сервер проверяет переподключение**
```
RTSPlayer.CmdRegisterClient(nickname, email)
  → FindPlayerByNickname(nickname)
  → Если найден:
      - Уничтожить новый player object
      - Переназначить соединение существующему игроку
      - ReassignOwnership() - вернуть юниты и здания
  → Если не найден:
      - SetNickname(nickname)
      - SetAccountEmail(email)
      - Это новый игрок
```

### **5. Отключение клиента**
```
OnServerDisconnect()
  → RemoveClientAuthorityFromPlayerObjects()  // Снять authority
  → НЕ уничтожать player object
  → Автосохранение мира (nickname сохраняется в JSON)
```

### **6. Переподключение**
```
Тот же игрок (nickname) подключается снова
  → CmdRegisterClient() находит старого игрока
  → Восстанавливает соединение
  → ReassignOwnership() возвращает юниты
  → Игрок получает всё обратно!
```

---

## 🔨 Настройка в Unity Inspector

### **Main Menu Scene:**

#### **1. Создать пустой GameObject "Account System"**
- Add Component → **RTS Account Config**
  - Website Url: `http://localhost:3000` (или ваш URL)

- Add Component → **RTS Account Client**
  (никаких настроек не требуется)

#### **2. Создать UI для авторизации** (опционально)

**Вариант A: Простая кнопка**
```
Canvas
  └─ Button "Sign In"
       OnClick() → RTSAccountClient.BeginSignIn()
```

**Вариант B: Полноценная панель** (как в KaplaoGames)
```
Canvas
  └─ Panel "Account Panel"
       ├─ Text "Status" (показывает RTSAccountClient.StatusMessage)
       ├─ Button "Sign In"  → RTSAccountClient.BeginSignIn()
       ├─ Button "Sign Out" → RTSAccountClient.SignOut()
       └─ Text "Nickname" (показывает RTSPlayerSession.Nickname)
```

#### **3. Подписка на события** (C# скрипт)
```csharp
void Start()
{
    if (RTSAccountClient.Instance != null)
    {
        RTSAccountClient.Instance.SignedIn += OnSignedIn;
        RTSAccountClient.Instance.SignedOut += OnSignedOut;
        RTSAccountClient.Instance.StatusChanged += OnStatusChanged;
    }
}

void OnSignedIn()
{
    Debug.Log($"Signed in as: {RTSPlayerSession.Nickname}");
    nicknameText.text = RTSPlayerSession.Nickname;
}

void OnSignedOut()
{
    Debug.Log("Signed out");
    nicknameText.text = "Guest";
}

void OnStatusChanged()
{
    statusText.text = RTSAccountClient.Instance.StatusMessage;
}
```

---

## 📝 Изменённые файлы

### **RTSPlayer.cs**
**Добавлено:**
- `nickname` и `accountEmail` SyncVar поля
- `CmdRegisterClient()` - Command для регистрации клиента
- `FindPlayerByNickname()` - поиск игрока по nickname
- Вызов `CmdRegisterClient()` в `Start()` для локального игрока

### **RTSWorldSaveData.cs**
**Добавлено в `PlayerSaveData`:**
- `nickname` - ник игрока
- `accountEmail` - email аккаунта

### **WorldSnapshotService.cs**
**Изменено в `Capture()`:**
```csharp
playerData.nickname = p.Nickname;
playerData.accountEmail = p.AccountEmail;
```

### **RTSNetworkManager.cs**
**Добавлено:**
- `ReassignOwnershipPublic()` - публичный метод для переназначения владельца

### **MainMenuUI.cs**
**Изменено:**
```csharp
RTSPlayerSession.Load();  // вместо PlayerSession.Load()
```

---

## 🌐 Веб-сайт (KaplaoGames Website)

### API Endpoints используемые:

#### **POST /api/device/start**
Создаёт новый device code для авторизации.

**Response:**
```json
{
  "deviceCode": "ABC123",
  "pollIntervalMs": 1000
}
```

#### **GET /api/device/poll?code=ABC123**
Проверяет статус авторизации.

**Response (pending):**
```json
{
  "status": "pending"
}
```

**Response (ready):**
```json
{
  "status": "ready",
  "token": "jwt_token_here",
  "nickname": "PlayerName",
  "email": "player@example.com"
}
```

#### **GET /device-signin?device=ABC123**
Страница входа в браузере (игрок входит здесь).

---

## 🎯 Использование БЕЗ UI

Если **НЕ хочешь делать UI** для авторизации, игроки будут использовать nickname по умолчанию "Player". Это всё равно работает:

1. **Без авторизации:**
   - Nickname = "Player" (или другой локальный ник)
   - Переподключение работает пока игра запущена
   - После перезапуска игры - новый игрок

2. **С авторизацией через веб-сайт:**
   - Игрок входит → получает уникальный nickname
   - Может переподключаться даже после перезапуска игры
   - Юниты сохраняются между сессиями

---

## 🧪 Тестирование

### **Тест 1: Авторизация**
1. Запустить KaplaoGames Website (`npm run dev` в папке website)
2. Открыть RTSokak → Main Menu
3. Нажать "Sign In"
4. Войти на сайте
5. Нажать "Sign In" снова в игре
6. ✅ Должен авторизоваться и показать nickname

### **Тест 2: Переподключение**
1. **Хост** создаёт мир
2. **Клиент** авторизуется (получает nickname "TestPlayer")
3. **Клиент** подключается, спавнит юниты
4. **Клиент** отключается (Alt+F4)
5. ✅ **Хост видит:** юниты TestPlayer остались
6. **Клиент** подключается снова
7. ✅ **Клиент видит:** все свои юниты вернулись!

### **Тест 3: Сохранение/Загрузка**
1. **Хост** создаёт мир "TestWorld"
2. **Клиент** (nickname="TestPlayer") подключается, строит юнитов
3. **Клиент** отключается → автосохранение
4. **Хост** выходит из игры
5. **Хост** загружает "TestWorld"
6. ✅ Юниты TestPlayer появляются на карте
7. **Клиент** (nickname="TestPlayer") подключается
8. ✅ Получает своих юнитов обратно!

---

## 🔍 Отладка

### Логи для проверки:

**При авторизации:**
```
[RTSAccount] BeginSignIn baseUrl=http://localhost:3000
[RTSAccount] Opening sign-in: http://localhost:3000/device-signin?device=ABC123
[RTSAccount] poll HTTP 200 result=Success body={"status":"ready",...}
[RTSAccount] Signed in as TestPlayer / te*****@gmail.com
```

**При подключении нового игрока:**
```
[RTSPlayer] New player registered: 'TestPlayer' (email=test@gmail.com)
```

**При переподключении:**
```
[RTSPlayer] Reconnecting player 'TestPlayer' (email=test@gmail.com), playerIndex=1
[RTSNetworkManager] Reassigning ownership for playerIndex=1 to connectionId=2
[RTSNetworkManager] Reassigned 5 units and 2 buildings
```

---

## ⚠️ Важные замечания

1. **Веб-сайт должен быть запущен** (`localhost:3000` или другой URL в `RTSAccountConfig`)

2. **Nickname регистрируется АВТОМАТИЧЕСКИ** при подключении через `CmdRegisterClient()`

3. **Старая система по IP всё ещё работает** как fallback (если nickname пустой)

4. **Регистрация через тот же сайт** что и KaplaoGames (можно использовать один аккаунт)

5. **UI создавать не обязательно** - система работает и без кнопок "Sign In"

---

## 📦 Что нужно создать в Inspector

### Минимальная настройка:

**Main Menu Scene:**
```
GameObject "Account System"
  - RTSAccountConfig (Website Url: http://localhost:3000)
  - RTSAccountClient
```

**Опционально - UI:**
```
Canvas → Panel "Account UI"
  - Button "Sign In"  → OnClick: RTSAccountClient.BeginSignIn()
  - Button "Sign Out" → OnClick: RTSAccountClient.SignOut()
  - Text "Status"     (обновляется через StatusChanged event)
  - Text "Nickname"   (показывает RTSPlayerSession.Nickname)
```

---

## ✅ Готово!

Теперь у тебя есть полноценная система авторизации через веб-сайт KaplaoGames с переподключением по nickname!
