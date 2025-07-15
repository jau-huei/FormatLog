# FormatLog 日志组件方案

## 方案简介

本方案包含两个项目：
- **FormatLog**：高性能结构化日志组件，支持参数化、格式去重、调用上下文、批量写入、游标分页查询。
- **DemoWPF**：WPF 演示项目，展示日志写入、查询、富文本渲染等功能。

---

## FormatLog 组件核心

### 主要功能
- **结构化日志格式**：通过 `Format` 唯一约束，日志内容参数化，便于检索分析。
- **参数化日志**：`Log` 支持最多 10 个参数，自动归档参数表，避免重复存储。
- **调用上下文追踪**：`CallerInfo` 记录成员、文件、行号，便于定位。
- **高性能批量写入**：`FLog` 双缓冲队列+后台线程，自动批量写入 SQLite，进程退出自动 Flush。
- **高效分页查询**：支持游标分页、双向翻页、条件筛选（级别、时间、格式、参数、调用者等）。
- **异常持久化**：Flush 异常自动保存 JSON/TXT，便于排查。

### 组件结构
- `FormatLog/Format.cs`  日志格式定义，唯一性约束，批量插入 SQL。
- `FormatLog/Log.cs`  日志主实体，包含格式、参数、调用上下文、创建时间等。
- `FormatLog/Argument.cs`  日志参数实体，唯一性约束。
- `FormatLog/CallerInfo.cs`  调用上下文实体，唯一性约束。
- `FormatLog/LogDbContext.cs`  EF Core 数据库上下文，自动建表，按日期分库。
- `FormatLog/FLog.cs`  日志池管理、批量写入、异常处理、分页查询。
- 其他辅助类：`QueryModel`、`FlushInfo`、`KeysetPage`。

### 快速入门

1. 写入日志 
```code
FLog.Add(new Log(LogLevel.Info, "用户登录：{0}@{1}", userName, domain).WithCallerInfo());
```
2. 查询日志（分页、筛选） 
```code 
    var query = new QueryModel()
        .WithLevel(LogLevel.Info)
        .WithFormat("登录")
        .OrderBy(OrderType.OrderByIdDescending)
        .WithCursorId(nextCursorId);

    var page = await query.KeysetPaginationAsync();
```

### 设计亮点
- **格式去重**：日志格式、参数、调用上下文均自动去重，节省存储空间。
- **批量高效**：双缓冲队列+批量 SQL 插入，极大提升写入性能。
- **易扩展**：支持自定义筛选、分页、异常处理。
- **数据库分库**：按天分库，便于归档和维护。

### 适用场景
- 高并发日志写入的桌面/服务端应用
- 结构化日志分析、检索系统
- 需要追踪调用上下文、参数的调试/运维场景

### 依赖
- .NET 8
- Microsoft.EntityFrameworkCore.Sqlite

---

## DemoWPF 演示项目

### 主要功能
- **日志写入演示**：支持多种类型日志（系统信息、乘法、除法、随机字符串、长/短文本、时间戳、用户/磁盘/网络信息等）批量写入。
- **日志查询与筛选**：支持按格式、参数、调用者、级别、时间范围等多条件筛选，支持游标分页、双向翻页。
- **富文本渲染**：日志内容参数高亮显示，支持分段富文本。
- **性能统计**：实时显示日志写入性能（每百条写入耗时）。

### 主要界面
- 日志写入页：选择日志类型和等级，批量写入演示。
- 日志查询页：多条件筛选、分页浏览、富文本高亮。

### 运行方式
1. 安装 .NET 8 SDK。
2. 运行 `DemoWPF` 项目（WinExe，WPF）。
3. 依赖 MahApps.Metro（UI美化）、FormatLog（日志核心）。

### 集成 FormatLog 步骤
1. 引用 FormatLog 项目或 NuGet 包。
2. 使用 `FLog.Add(new Log(...).WithCallerInfo())` 写入日志。
3. 使用 `QueryModel` 进行分页查询。
4. WPF 可用 `LogViewModel` 进行富文本分段渲染。

### 目录结构
```plaintext
FormatLog/
  ├─ Format.cs
  ├─ Log.cs
  ├─ Argument.cs
  ├─ CallerInfo.cs
  ├─ LogDbContext.cs
  ├─ FLog.cs
DemoWPF/
  ├─ LogViewModel.cs
  ├─ LogTextSegment.cs
  ├─ MainWindow.xaml(.cs)
```
### 依赖
- .NET 8
- Microsoft.EntityFrameworkCore.Sqlite
- MahApps.Metro（DemoWPF UI）

### 常见问题
- 日志写入异常会自动持久化到 JSON/TXT 文件，便于排查。
- 日志数据库按天分库，便于归档和维护。

---

如需详细 API 文档或二次开发建议，请查阅源码注释或联系维护者。
