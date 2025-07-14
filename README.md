# FormatLog 日志组件方案

## 方案简介

本方案包含两个项目：
- **FormatLog**：高性能结构化日志组件，支持参数化、格式去重、调用上下文、批量写入、游标分页查询。
- **DemoWPF**：WPF 演示项目，展示日志写入、查询、富文本渲染等功能。

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

1. **写入日志**
```csharp
FLog.Add(new Log(LogLevel.Info, "用户登录：{0}@{1}", userName, domain).WithCallerInfo());
```

2. **查询日志（分页、筛选）**
```csharp
var query = new QueryModel()
    .WithLevel(LogLevel.Error)
    .WithFormat("登录")
    .OrderBy(OrderType.OrderByIdDescending)
    .WithCursorId(nextCursorId);
var page = await query.KeysetPaginationAsync();
```

3. **WPF 显示日志**
- 使用 `LogViewModel` 解析分段，支持富文本高亮参数。

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

### 目录结构
```
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

---

如需详细 API 文档或二次开发建议，请查阅源码注释或联系维护者。
