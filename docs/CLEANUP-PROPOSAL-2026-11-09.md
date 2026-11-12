# 工程缓存清理待确认清单

状态：用户已明确确认清理这26个缓存目录，已全部执行。共删除492个文件，逻辑大小414.48MiB；随后实际工程Release构建成功，0警告/0错误。构建重新生成8.75MiB中间文件，目录文件逻辑大小净减少405.73MiB。

实际源码 D:\DockForWindows 无 .git，不能取得该目录的 Git 状态；会话入口存在无提交 Git 仓库，本轮文件均为未跟踪，全部保留。正式源码、原始备份、脚本、文档、图片/字体/图标/Shader资源、所有 data、配置/锁文件、tools SDK/NuGet依赖缓存和 packaging 均不列入清理。

## 已批准并执行的原缓存清单

26 个目录，按文件逻辑长度估计 414.48 MiB（0.43 GB），实际磁盘释放量会受压缩/簇大小/占用影响。均来自编译输出或中间文件；obj 中的 *.g.cs / AssemblyInfo.cs 为自动生成文件，正式源文件不动。目录内未发现 data/userdata/logs/publish 子目录；执行前仍需重新检查新文件、符号链接和运行占用。

| 完整路径 | MiB | 原因 |
|---|---:|---|
| `D:\DockForWindows\src\DockForWindows\obj` | 27.98 | 可由同一工程与本地 SDK/依赖重新生成的编译中间文件 |
| `D:\DockForWindows\tests\DockForWindows.Tests\obj` | 0.87 | 可由同一工程与本地 SDK/依赖重新生成的编译中间文件 |
| `D:\DockForWindows\tests\DockForWindows.Tests\bin\Debug` | 30.02 | 隔离测试或旧测试的可重建二进制输出 |
| `D:\DockForWindows\tests\probe-build\bin` | 0.49 | 隔离测试或旧测试的可重建二进制输出 |
| `D:\DockForWindows\tests\probe-build\obj` | 0.47 | 可由同一工程与本地 SDK/依赖重新生成的编译中间文件 |
| `D:\DockForWindows\tests\wgc-build\obj` | 0.41 | 可由同一工程与本地 SDK/依赖重新生成的编译中间文件 |
| `C:\Users\HENG\Documents\ChatGPT\DockForWindows\rendering-lab\A\src\DockForWindows\bin` | 30.57 | 隔离测试或旧测试的可重建二进制输出 |
| `C:\Users\HENG\Documents\ChatGPT\DockForWindows\rendering-lab\A\src\DockForWindows\obj` | 8.74 | 可由同一工程与本地 SDK/依赖重新生成的编译中间文件 |
| `C:\Users\HENG\Documents\ChatGPT\DockForWindows\rendering-lab\A\tests\DockForWindows.Tests\bin` | 30.93 | 隔离测试或旧测试的可重建二进制输出 |
| `C:\Users\HENG\Documents\ChatGPT\DockForWindows\rendering-lab\A\tests\DockForWindows.Tests\obj` | 0.52 | 可由同一工程与本地 SDK/依赖重新生成的编译中间文件 |
| `C:\Users\HENG\Documents\ChatGPT\DockForWindows\rendering-lab\C\src\DockForWindows\bin` | 30.57 | 隔离测试或旧测试的可重建二进制输出 |
| `C:\Users\HENG\Documents\ChatGPT\DockForWindows\rendering-lab\C\src\DockForWindows\obj` | 8.75 | 可由同一工程与本地 SDK/依赖重新生成的编译中间文件 |
| `C:\Users\HENG\Documents\ChatGPT\DockForWindows\rendering-lab\C\tests\DockForWindows.Tests\bin` | 30.96 | 隔离测试或旧测试的可重建二进制输出 |
| `C:\Users\HENG\Documents\ChatGPT\DockForWindows\rendering-lab\C\tests\DockForWindows.Tests\obj` | 0.55 | 可由同一工程与本地 SDK/依赖重新生成的编译中间文件 |
| `C:\Users\HENG\Documents\ChatGPT\DockForWindows\animation-lab\A\src\DockForWindows\bin` | 30.57 | 隔离测试或旧测试的可重建二进制输出 |
| `C:\Users\HENG\Documents\ChatGPT\DockForWindows\animation-lab\A\src\DockForWindows\obj` | 8.75 | 可由同一工程与本地 SDK/依赖重新生成的编译中间文件 |
| `C:\Users\HENG\Documents\ChatGPT\DockForWindows\animation-lab\A\tests\DockForWindows.Tests\bin` | 30.98 | 隔离测试或旧测试的可重建二进制输出 |
| `C:\Users\HENG\Documents\ChatGPT\DockForWindows\animation-lab\A\tests\DockForWindows.Tests\obj` | 0.57 | 可由同一工程与本地 SDK/依赖重新生成的编译中间文件 |
| `C:\Users\HENG\Documents\ChatGPT\DockForWindows\animation-lab\C\src\DockForWindows\bin` | 30.58 | 隔离测试或旧测试的可重建二进制输出 |
| `C:\Users\HENG\Documents\ChatGPT\DockForWindows\animation-lab\C\src\DockForWindows\obj` | 8.76 | 可由同一工程与本地 SDK/依赖重新生成的编译中间文件 |
| `C:\Users\HENG\Documents\ChatGPT\DockForWindows\animation-lab\C\tests\DockForWindows.Tests\bin` | 30.98 | 隔离测试或旧测试的可重建二进制输出 |
| `C:\Users\HENG\Documents\ChatGPT\DockForWindows\animation-lab\C\tests\DockForWindows.Tests\obj` | 0.57 | 可由同一工程与本地 SDK/依赖重新生成的编译中间文件 |
| `C:\Users\HENG\Documents\ChatGPT\DockForWindows\animation-lab\P\src\DockForWindows\bin` | 30.57 | 隔离测试或旧测试的可重建二进制输出 |
| `C:\Users\HENG\Documents\ChatGPT\DockForWindows\animation-lab\P\src\DockForWindows\obj` | 8.75 | 可由同一工程与本地 SDK/依赖重新生成的编译中间文件 |
| `C:\Users\HENG\Documents\ChatGPT\DockForWindows\animation-lab\P\tests\DockForWindows.Tests\bin` | 30.99 | 隔离测试或旧测试的可重建二进制输出 |
| `C:\Users\HENG\Documents\ChatGPT\DockForWindows\animation-lab\P\tests\DockForWindows.Tests\obj` | 0.58 | 可由同一工程与本地 SDK/依赖重新生成的编译中间文件 |

## 明确保留

- D:\DockForWindows\src\DockForWindows\bin 全部保留：Debug 为当前运行实例，Release 为新验证构建。
- D:\DockForWindows\tests\DockForWindows.Tests\bin\Release 保留：新定向检查产物。
- 正式发布包、安装程序、所有测试截图/JSON/CSV报告、源码快照、回退原件均保留。
- 未发现根目录 .vs；不清理其他软件或系统缓存，不使用 git clean / reset。
- 旧 ETW 原始数据仍可能用于剩余 GPU/呈现问题的再分析，保留 2526.38 MiB：
  - `C:\Users\HENG\Documents\ChatGPT\DockForWindows\rendering-lab\gpu-A.etl`（975.00 MiB）
  - `C:\Users\HENG\Documents\ChatGPT\DockForWindows\rendering-lab\gpu-B.etl`（474.13 MiB）
  - `C:\Users\HENG\Documents\ChatGPT\DockForWindows\rendering-lab\gpu-C.etl`（927.38 MiB）
  - `C:\Users\HENG\Documents\ChatGPT\DockForWindows\rendering-lab\light-A.etl`（47.63 MiB）
  - `C:\Users\HENG\Documents\ChatGPT\DockForWindows\rendering-lab\light-B.etl`（53.25 MiB）
  - `C:\Users\HENG\Documents\ChatGPT\DockForWindows\rendering-lab\light-C.etl`（49.00 MiB）

执行记录：删除前逐项验证26个绝对路径与原清单相符，检查了祖先/内部重解析点、持久化数据、意外源文件和运行占用。仅删除批准目录。tools/Build.ps1 -Configuration Release 构建通过；原版Shader与新嵌入资源哈希一致，已接受源码哈希未变，6个ETW原始文件大小未变。实际工程src/DockForWindows/obj已由构建重新生成，其余25个清单目录未重建。没有关闭程序或设置窗口，没有重跑无变化的旧功能测试。结构化记录见会话animation-lab/cleanup-preflight.json与cleanup-result.json。
