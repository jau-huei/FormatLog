# FormatLog ��־�������

## �������

����������������Ŀ��
- **FormatLog**�������ܽṹ����־�����֧�ֲ���������ʽȥ�ء����������ġ�����д�롢�α��ҳ��ѯ��
- **DemoWPF**��WPF ��ʾ��Ŀ��չʾ��־д�롢��ѯ�����ı���Ⱦ�ȹ��ܡ�

## FormatLog �������

### ��Ҫ����
- **�ṹ����־��ʽ**��ͨ�� `Format` ΨһԼ������־���ݲ����������ڼ���������
- **��������־**��`Log` ֧����� 10 ���������Զ��鵵�����������ظ��洢��
- **����������׷��**��`CallerInfo` ��¼��Ա���ļ����кţ����ڶ�λ��
- **����������д��**��`FLog` ˫�������+��̨�̣߳��Զ�����д�� SQLite�������˳��Զ� Flush��
- **��Ч��ҳ��ѯ**��֧���α��ҳ��˫��ҳ������ɸѡ������ʱ�䡢��ʽ�������������ߵȣ���
- **�쳣�־û�**��Flush �쳣�Զ����� JSON/TXT�������Ų顣

### ����ṹ
- `FormatLog/Format.cs`  ��־��ʽ���壬Ψһ��Լ������������ SQL��
- `FormatLog/Log.cs`  ��־��ʵ�壬������ʽ�����������������ġ�����ʱ��ȡ�
- `FormatLog/Argument.cs`  ��־����ʵ�壬Ψһ��Լ����
- `FormatLog/CallerInfo.cs`  ����������ʵ�壬Ψһ��Լ����
- `FormatLog/LogDbContext.cs`  EF Core ���ݿ������ģ��Զ����������ڷֿ⡣
- `FormatLog/FLog.cs`  ��־�ع�������д�롢�쳣������ҳ��ѯ��
- ���������ࣺ`QueryModel`��`FlushInfo`��`KeysetPage`��

### ��������

1. **д����־**
```csharp
FLog.Add(new Log(LogLevel.Info, "�û���¼��{0}@{1}", userName, domain).WithCallerInfo());
```

2. **��ѯ��־����ҳ��ɸѡ��**
```csharp
var query = new QueryModel()
    .WithLevel(LogLevel.Error)
    .WithFormat("��¼")
    .OrderBy(OrderType.OrderByIdDescending)
    .WithCursorId(nextCursorId);
var page = await query.KeysetPaginationAsync();
```

3. **WPF ��ʾ��־**
- ʹ�� `LogViewModel` �����ֶΣ�֧�ָ��ı�����������

### �������
- **��ʽȥ��**����־��ʽ�����������������ľ��Զ�ȥ�أ���ʡ�洢�ռ䡣
- **������Ч**��˫�������+���� SQL ���룬��������д�����ܡ�
- **����չ**��֧���Զ���ɸѡ����ҳ���쳣����
- **���ݿ�ֿ�**������ֿ⣬���ڹ鵵��ά����

### ���ó���
- �߲�����־д�������/�����Ӧ��
- �ṹ����־����������ϵͳ
- ��Ҫ׷�ٵ��������ġ������ĵ���/��ά����

### ����
- .NET 8
- Microsoft.EntityFrameworkCore.Sqlite

### Ŀ¼�ṹ
```
FormatLog/
  ���� Format.cs
  ���� Log.cs
  ���� Argument.cs
  ���� CallerInfo.cs
  ���� LogDbContext.cs
  ���� FLog.cs
DemoWPF/
  ���� LogViewModel.cs
  ���� LogTextSegment.cs
  ���� MainWindow.xaml(.cs)
```

---

������ϸ API �ĵ�����ο������飬�����Դ��ע�ͻ���ϵά���ߡ�
