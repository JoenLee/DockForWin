# Microsoft To Do / OneNote 小组件接入

本版本支持 Microsoft 设备代码登录。Microsoft To Do 读取清单、任务、截止日期与完成状态，桌面小组件可切换完成状态；OneNote 只读页面与正文，点击桌面笔记可在 OneNote 网页中编辑。数据每分钟更新，账户缓存与本地待办、笔记分开，预览不会完成任务。预览中的音乐控制会直接控制当前系统媒体会话。

## 注册应用（只需一次）

1. 打开 [Microsoft Entra 管理中心](https://entra.microsoft.com/)，进入“应用程序 → 应用注册 → 新注册”。
2. 名称填写 `DockForWin`。账户类型选择“任何组织目录中的账户和个人 Microsoft 账户”。如果只供组织内部使用，可按组织策略选择类型；组织也可能禁止设备代码登录。
3. 创建后复制“应用程序（客户端）ID”。这不是密码，不需要创建或填写客户端密钥。
4. 在“身份验证 → 高级设置”中，将“允许公共客户端流”设为“是”，保存。设备代码流程不需要回调地址。
5. 在“API 权限 → 添加权限 → Microsoft Graph → 委托的权限”中添加 `Tasks.ReadWrite`、`Notes.Read`、`offline_access`。使用委托权限，不能用应用程序权限替代。组织策略可能要求管理员同意。

## 在 DockForWin 中登录

1. 进入“小组件 → 提醒事项或备忘录 → 数据源 → Microsoft 账户…”。
2. 填入客户端 ID，点击“登录”。
3. 点击“打开 Microsoft 登录”，在 Microsoft 页面输入面板显示的设备代码，然后登录并同意授权。也可以手动打开面板中的网址。不要把账户密码输入 DockForWin。
4. 登录成功后，在该模板的数据源勾选 `Microsoft To Do` 或 `OneNote`。列表筛选可按清单名或 OneNote 分区名筛选，留空显示全部。
5. 点击设置顶部的“保存设置”。新放置的桌面小组件会复制当前数据源；已有组件可通过右键“组件设置…”切换来源。

退出账户会删除此 Windows 用户、此数据目录对应的刷新凭据并清空云端内存缓存。刷新凭据保存在 Windows 凭据管理器，不写入 `settings.json` 或仓库。客户端 ID 单独保存在运行数据目录的 `widget-account/microsoft-client.json`。未授权、网络错误或服务拒绝会显示错误，不使用本地数据冒充云端数据。

读取上限为 100 个 To Do 清单、500 个任务、最近修改的 30 页 OneNote；组件按尺寸显示其中一部分。历史音乐项目仅显示观察到的记录，系统媒体会话不提供任意历史重播。

## 天气定位

在天气或含天气的日历模板“数据源”中选择“使用当前位置”。Windows 必须开启位置服务并允许桌面应用访问。定位和天气默认每 15 分钟更新，定位失败后 5 分钟重试；也可以手动刷新或选择城市。不会用 IP 地址猜测位置，也不会填写虚构天气。

## 验证范围

本次提供离线协议与渲染验证；真实账户登录、组织策略和当前机器的定位权限仍需在配置客户端 ID 后检查。测试中的账户数据与媒体图片都是明确的测试替身，不表示已读取你的账户。

依据：[设备代码授权](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-device-code)、[To Do 任务接口](https://learn.microsoft.com/en-us/graph/api/todotasklist-list-tasks?view=graph-rest-1.0)、[OneNote 页面接口](https://learn.microsoft.com/en-us/graph/api/onenote-list-pages?view=graph-rest-1.0)、[Windows 定位授权](https://learn.microsoft.com/en-us/uwp/api/windows.devices.geolocation.geolocator.requestaccessasync)。
