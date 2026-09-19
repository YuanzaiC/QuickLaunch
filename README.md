# QuickLaunch

Windows 11 风格的自定义快速启动面板，用于替代开始菜单/桌面快捷方式。（项目由AI创作）

### 使用

在项目目录执行：

```powershell
dotnet restore
dotnet build -c Release
dotnet run -c Release
```

输出目录通常位于：

```text
bin\Release\net8.0-windows\win-x64\publish\
```

建议把 `config.json` 与 exe 放在同一目录。

程序首次运行还会创建：

```text
favorites.json
recent.json
catalog.cache.json
cache/icons/*.png
logs/quicklaunch.log
```

# QuickLaunch 功能

快捷键呼出的应用启动器：常用程序集中到一个面板，打字就能找、回车就能开。

## 呼出与启动

### 全局快捷键呼出
任意程序里按 `Alt + Space` 都能呼出面板，快捷键可在设置里修改；`Esc` 或点击面板外即可收起。

### 单击或回车启动
鼠标点一下卡片直接启动；输入关键词后按回车打开当前项，没动过方向键时就是第一条结果。

### 以管理员身份运行
右键可选“以管理员身份运行”，先弹出应用内确认，再由 Windows UAC 决定是否继续。

## 搜索与排序

### 拼音与首字母搜索
支持名称、完整路径、拼音和拼音首字母，输入 `wxz` 也能找到“外星仔加速器”。

### 智能排序
没有关键词时按 收藏 → 最近使用 → 使用次数 → 手动顺序排列；输入关键词后按匹配度从高到低排。

### 按文件夹分组
主列表按应用所在文件夹自动分组，把卡片拖到另一个卡片上即可归组并调整组内顺序，不会移动磁盘上的真实文件。

## 收藏与最近

### 收藏置顶
右键“添加到收藏”，应用就固定在面板顶部的收藏区，一键打开；再点一次即可取消。

### 最近打开
启动过的应用会自动进入“最近打开”区，可以单独移除某一项。

### 顶部卡片同样可右键
收藏 / 最近的小卡片与主列表共用同一套右键菜单，一次右键即可弹出。

## 右键菜单

### 一次右键即弹出
右键一下立刻出菜单，不需要“先点一下选中、再右键一次”。

### 菜单里的操作
启动、以管理员身份运行、添加到收藏 / 取消收藏、从最近打开中移除、打开所在文件夹、复制完整路径、重命名、在列表中隐藏。

## 界面与交互

### 浅色 / 深色 / 跟随系统
三档主题随点随生效，强调色自动跟随 Windows 个性化设置。

### 悬停反馈，不留选中
鼠标经过只出现悬停底色，移开就恢复；单击直接启动，不会永远亮着一项。

### 键盘光标
只有真的按了方向键才显示当前项的高亮，鼠标一动就让位给悬停效果。

### 失效快捷方式提示
快捷方式指向的文件不存在时，卡片上显示警告标记，启动前给出提示而不是静默失败。

## 托盘与系统

### 托盘驻留
收起面板后可驻留托盘，双击图标再次呼出；可在设置里关闭，关闭后仅靠快捷键呼出。

### 开机自启
写入当前用户的启动项，不需要管理员权限，可随时开关。

## 设置

### 扫描与索引
添加 / 移除扫描目录，设置是否递归、扫描深度、包含的扩展名与排除的文件夹，修改呼出快捷键。

### 外观与行为
主题、开机自启、最小化时是否驻留托盘。

### 维护与数据
清除图标缓存、清理失效项、恢复已隐藏项、导入 / 导出设置、打开数据文件夹、退出程序。
## 目录

```text
QuickLaunch/
├─ Assets/
│  ├─ QuickLaunch.ico
│  ├─ QuickLaunch.png
│  └─ default-icon.png
├─ Helpers/
│  ├─ AppDialog.cs
│  ├─ FuzzyMatcher.cs
│  ├─ NativeMethods.cs
│  ├─ PinyinSearchService.cs
│  ├─ SimpleInputDialog.cs
│  ├─ UiAnimations.cs
│  └─ WindowDragHelper.cs
├─ Models/
│  ├─ AppConfig.cs
│  ├─ AppEntry.cs
│  └─ AppGroup.cs
├─ Services/
│  ├─ ConfigService.cs
│  ├─ FileWatcherService.cs
│  ├─ HotkeyService.cs
│  ├─ IconCacheService.cs
│  ├─ LaunchService.cs
│  ├─ LogService.cs
│  ├─ PersistenceService.cs
│  ├─ ScannerService.cs
│  ├─ SingleInstanceService.cs
│  ├─ StartupService.cs
│  └─ ThemeService.cs
├─ Themes/
│  ├─ Controls.xaml
│  └─ Tokens.xaml
├─ ViewModels/
│  ├─ MainViewModel.cs
│  ├─ SettingsViewModel.cs
│  └─ ViewModelBase.cs
├─ App.xaml
├─ App.xaml.cs
├─ Converters.cs
├─ MainWindow.xaml
├─ MainWindow.xaml.cs
├─ SettingsWindow.xaml
├─ SettingsWindow.xaml.cs
├─ QuickLaunch.csproj
├─ app.manifest
└─ config.json
```
