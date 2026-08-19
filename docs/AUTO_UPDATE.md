# WordReader 自动更新

WordReader 启动后会在后台读取 GitHub 仓库的最新正式 Release。只有线上版本高于本地版本时才会提示，用户可以选择“更新”或“稍后”，不会强制更新。也可以在命令栏输入 `/更新`（英文界面为 `/update`）手动检查。

## 发布新版本

自动更新以 GitHub Release 为准，不直接使用 `main` 分支源码。这样可以确保用户下载的是已经构建和验证过的完整程序，而不是无法直接运行的源码快照。

发布时需要同时完成以下事项：

1. 在 `UpdateService.cs` 中递增 `AssemblyVersion`、`AssemblyFileVersion` 和 `AssemblyInformationalVersion`。
2. 构建完整的 Windows x64 便携包，ZIP 文件名使用 `WordReader-v<版本>-win-x64-portable.zip`。
3. 为 ZIP 生成 SHA-256 文件，文件名中包含 `SHA256`，内容格式为 `<64位哈希>  <ZIP文件名>`。
4. 创建非草稿、非预发布的 GitHub Release，标签使用 `v<主版本>.<次版本>.<修订版本>`，并上传 ZIP 与 SHA-256 文件。

示例：

```text
Tag: v1.1.0
WordReader-v1.1.0-win-x64-portable.zip
WordReader-v1.1.0-SHA256.txt
```

GitHub 目前也会为 Release 附件返回 `sha256:<哈希>` 摘要。程序优先使用该摘要，并在缺失时读取独立的 SHA-256 文件。没有可信校验值的更新会被拒绝。

## 更新过程

1. 程序通过 GitHub 官方 API 获取最新正式 Release 并比较语义版本。
2. 用户点击“更新”后，程序把便携 ZIP 下载到当前用户的临时目录。
3. SHA-256 校验通过后，程序启动独立更新进程并退出。
4. 更新进程备份将被覆盖的文件、应用新版本并重启 WordReader。
5. 覆盖失败时，更新进程会恢复备份并显示错误信息。

登录状态保存在 `%LOCALAPPDATA%\QuietReader\WebView2`，不在便携安装目录中，更新不会覆盖登录资料。
