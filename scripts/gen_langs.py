# -*- coding: utf-8 -*-
"""生成 WPF 语言资源字典 Assets/Langs/*.xaml
主字典 zh-CN；其他语言缺失的 key 由回退链处理（当前语言 → en-US → zh-CN）。
用法: python scripts/gen_langs.py
"""
import os, io

OUT = os.path.join(os.path.dirname(__file__), "..", "src", "CleanMaster.App", "Assets", "Langs")

# ============ zh-CN 主字典 ============
ZH = {
    # 通用
    "common.selected": "已选择", "common.freeable": "可释放", "common.viewall": "查看全部",
    "common.loading": "正在读取…", "common.scanning": "正在扫描…", "common.n.items": "{0} 项",
    "common.approx": "约 ", "btn.cancel": "取消", "btn.exit": "退出", "btn.refresh": "刷新",
    "btn.stopscan": "停止扫描", "btn.rescan": "重新扫描", "btn.restore": "恢复", "btn.deleteperm": "永久删除",
    "btn.uninstall": "卸载", "btn.recycle": "移入回收站", "btn.remove": "移除", "btn.analyze.start": "开始分析",
    "btn.scan.now": "立即扫描", "btn.more": "更多功能", "btn.clean.selected": "清理已选项目",
    "btn.clean.start": "开始清理", "btn.done.home": "返回首页", "btn.done.quarantine": "打开隔离区",
    "btn.emptybin": "清空回收站", "btn.openbin": "打开回收站", "btn.scangroup": "扫描本组",
    "btn.cleansel": "清理所选项", "btn.dupscan": "开始扫描", "btn.dup.rescan": "重新扫描",
    "btn.dupkeepnew": "保留最新，选中其余", "btn.dupclean": "把选中的移入回收站",
    "btn.clearhistory": "清空历史", "btn.clearlocal": "清除本地历史记录", "btn.purge": "清空隔离区",
    "btn.shredfile": "选择文件粉碎", "btn.shreddir": "选择文件夹粉碎", "btn.shred": "永久粉碎",
    "tip.min": "最小化", "tip.max": "最大化", "tip.close": "关闭",
    "nav.home": "首页", "nav.scan": "智能扫描", "nav.clean": "清理空间", "nav.space": "空间分析",
    "nav.startup": "启动项", "nav.apps": "应用管理", "nav.toolbox": "工具箱", "nav.settings": "设置",
    # 风险/类别
    "risk.safe": "安全", "risk.low": "低风险", "risk.confirm": "需确认", "risk.high": "高风险",
    "cat.system": "系统", "cat.apps": "应用", "cat.images": "图片", "cat.videos": "视频",
    "cat.audio": "音频", "cat.documents": "文档", "cat.archives": "压缩包", "cat.other": "其他",
    # 时间
    "time.now": "刚刚", "time.minago": "{0} 分钟前", "time.today": "今天 {0}", "time.yesterday": "昨天 {0}",
    # 首页
    "home.slogan": "保持定期扫描，让电脑始终保持最佳状态",
    "home.status.good": "设备状态良好", "home.status.suggest": "建议清理一下", "home.status.tight": "磁盘空间紧张",
    "home.status.suggest.detail": "发现 {0} 可清理垃圾与 {1} 个高影响启动项",
    "home.status.tight.detail": "系统盘剩余空间不足 10%，建议立即清理",
    "home.status.lastscan": "上次扫描：{0}", "home.noscan": "还没有扫描过", "home.noscan.detail": "还没有扫描过，点击「立即扫描」开始",
    "home.card.junk": "可清理垃圾", "home.card.junk.note": "点击扫描查看可释放空间", "home.card.junk.lastscan": "上次扫描 {0}",
    "home.card.startup": "启动项", "home.card.startup.note": "个启动项随开机运行", "home.card.startup.note.high": "高影响启动项，可能拖慢开机",
    "home.card.large": "大文件", "home.card.large.note": "点击分析查找", "home.card.large.analyzed": "已分析",
    "home.card.large.analyzed.note": "点击查看大文件详情", "home.card.large.pending": "待分析",
    "home.card.apps": "已装应用", "home.card.apps.note": "点击管理已装应用",
    "home.disk.title": "磁盘空间 (C:)", "home.disk.title.disk": "磁盘空间 ({0})", "home.disk.total": "总容量 —",
    "home.disk.used": "已使用 —", "home.disk.free": "可用 —", "home.disk.total.f": "总容量 {0}",
    "home.disk.used.f": "已使用 {0} ({1:F1}%)", "home.disk.free.f": "可用 {0} ({1:F1}%)", "home.disk.more": "查看空间去哪了 →",
    "home.space.title": "空间分析", "home.space.empty": "还没有分析过这块磁盘",
    "home.activity.title": "最近活动", "home.activity.empty": "还没有活动记录，扫描清理一下试试",
    "home.suggest.title": "智能建议", "home.suggest.empty": "一切正常，没有需要处理的建议",
    "home.suggest.note.safe": "可安全清理，不影响系统使用", "home.suggest.clean": "立即清理", "home.suggest.optimize": "去优化",
    "home.suggest.startup.title": "开机启动建议", "home.suggest.startup.note": "发现 {0} 个高影响启动项，建议优化",
    "home.hero.note": "让电脑更干净&#x0a;运行更流畅！",
    # 智能扫描
    "scan.start": "开始扫描", "scan.cover": "一次扫描覆盖：系统垃圾 · 浏览器缓存 · 应用缓存 · 回收站",
    "scan.privacy": "扫描全程在本地进行；默认只勾选安全项目，高风险项目永远不会被自动选中。",
    "scan.preparing": "准备扫描…", "scan.found": "已发现 —", "scan.done": "扫描完成",
    "scan.reselectall": "全选安全项",
    "scan.group.safe": "可以安全清理", "scan.group.safe.note": "这些是系统与应用产生的临时数据，清理无风险",
    "scan.group.cache": "浏览器与应用缓存", "scan.group.cache.note": "缓存类数据，应用会在需要时重新下载",
    "scan.group.check": "建议检查", "scan.group.check.note": "清理前请确认这些内容不再需要",
    "result.title.found": "扫描完成，发现可释放 {0}", "result.title.clean": "扫描完成，很干净！",
    "result.sub": "{0} · 共扫描 {1} 类项目",
    "row.files": "{0} 个文件", "row.restore": "可进隔离区恢复", "row.direct": "直接删除",
    "row.running": "{0} 个相关进程运行中", "row.impact.tip": "清理影响：{0}",
    # 清理执行
    "clean.title": "正在清理…", "clean.freed": "已释放 —",
    "done.title": "清理完成", "done.cancelled": "清理已中断", "done.partial": "清理完成（部分跳过）",
    "done.freed.total": "共释放 {0}", "done.note.quarantine": "隔离区文件保留 {0} 天，可随时在工具箱中恢复。",
    "done.note.skip": "跳过的文件正在被其他程序使用，关闭相关程序后下次清理即可移除。",
    "done.stat.ok": "清理成功", "done.stat.quar": "进入隔离区", "done.stat.skip": "跳过（占用中）", "done.stat.fail": "失败",
    # 清理空间
    "clean.tab.system": "系统垃圾", "clean.tab.app": "应用缓存", "clean.tab.browser": "浏览器清理",
    "clean.tab.privacy": "隐私痕迹", "clean.tab.recycle": "回收站",
    "clean.tabnote.0": "系统与应用产生的临时文件、日志与崩溃报告，默认只勾选安全项目",
    "clean.tabnote.1": "聊天与办公软件的缓存和日志，不会触碰聊天记录与文档",
    "clean.tabnote.2": "浏览器为加速上网产生的缓存，不影响登录状态、密码与历史",
    "clean.tabnote.3": "涉及个人记录，默认全部不勾选，请仔细确认影响后再清理",
    "clean.tabnote.4": "清空回收站前请确认里面的文件不再需要",
    "clean.privacy.banner": "隐私项默认全部不勾选。勾选前请阅读每项的影响说明：清理 Cookie 会使网站退出登录，清理历史记录后不可恢复。",
    "clean.privacy.warn": "隐私项清理后无法恢复，请确认已了解各项影响。",
    "clean.recycle.desc": "清空后回收站中的文件将被永久删除、无法恢复。也可以先打开回收站检查一遍。",
    "clean.empty.before": "点击右上角「扫描」开始检查这一类项目", "clean.empty.clean": "这一类很干净，没有发现可清理的项目",
    "clean.empty.group": "点击下方「扫描本组」开始检查这一类项目",
    "recycle.info": "{0} 个项目，共占用 {1}", "recycle.empty": "回收站是空的",
    "recycle.confirm.title": "清空回收站？", "recycle.confirm.msg": "回收站中的 {0} 个文件（共 {1}）将被永久删除，无法恢复。",
    "toast.cleandone": "清理完成，释放 {0}", "toast.skipped": "（跳过 {0} 项）", "toast.binemptied": "回收站已清空",
    "toast.failed": "操作失败",
    "dlg.clean.selected.title": "确认清理所选项？", "dlg.clean.amount": "即将清理 {0} 的数据。",
    "dlg.clean.nrules": "即将清理 {0} 类项目，共 {1}。", "dlg.clean.confirmitems": "包含需要确认的项目：",
    "dlg.clean.quarantinehint": "可恢复类文件将进入隔离区。",
    "dlg.quickclean.title": "清理{0}？", "dlg.quickclean.msg": "预计可释放 {0}。\n可恢复类文件会进入隔离区。",
    # 空间分析
    "space.tab.overview": "概览", "space.tab.large": "大文件", "space.tree.title": "文件夹占用排行",
    "space.tree.hint": "点击展开子目录；已跳过系统链接与无权限目录",
    "space.empty": "还没有分析过这块磁盘，点击右上角「开始分析」（扫描期间可随时取消）",
    "space.scanning": "正在分析磁盘…", "space.cancelled": "已取消分析", "space.failed": "分析失败：",
    "space.done": "分析完成，用时 {0:F0} 秒", "space.rendererror": "分析结果渲染异常，详情见日志",
    "space.scanned": "已扫描 {0:N0} 个文件", "space.total": "共 {0}",
    "space.used.f": "已使用 {0} / {1}（{2:F1}%），可用 {3}", "space.used.f2": "已使用 {0} / {1}，可用 {2}",
    "space.analyzed.f": "分析于 {0} · {1:N0} 个文件 · {2:F0} 秒", "space.lastanalysis": "上次分析：{0}",
    "space.notanalyzed": "这块磁盘还没有分析过", "space.large.filter": "大小筛选",
    "space.large.none": "没有找到大于 {0:F0} MB 的文件", "space.modified": "修改于", "space.file": "文件",
    "space.recycle.title": "移入回收站？", "space.recycle.msg": "{0}（{1}）将移入回收站，可随时还原。",
    "toast.whitelisted": "已加入白名单，扫描与清理将跳过该文件", "toast.largecleaned": "清理了大文件 {0}",
    "toast.recycled": "已移入回收站，释放 {0}", "toast.recyclefailed": "移入回收站失败，文件可能被占用",
    # 启动项
    "startup.desc": "关闭启动项后应用仍可手动打开；所有修改都会记录原状态，可随时恢复。",
    "startup.col.toggle": "状态", "startup.col.impact": "启动影响", "startup.col.source": "来源", "startup.col.suggest": "建议",
    "startup.loading": "正在读取启动项…", "startup.empty": "没有匹配的启动项", "startup.systemchip": "系统组件",
    "startup.src.reghkcu": "注册表 (当前用户)", "startup.src.reghklm": "注册表 (本机)",
    "startup.src.folderuser": "启动文件夹 (用户)", "startup.src.foldercommon": "启动文件夹 (公共)",
    "startup.src.task": "计划任务", "startup.impact.high": "高", "startup.impact.med": "中",
    "startup.impact.low": "低", "startup.impact.unknown": "未测量",
    "startup.suggest.off": "可考虑关闭", "startup.suggest.system": "系统组件", "startup.suggest.keep": "保留",
    "startup.enabled": "已启用", "startup.disabled": "已禁用", "startup.toggled": "{0}启动项「{1}」",
    # 应用管理
    "apps.desc": "卸载将调用应用自带的卸载程序；卸载完成后可扫描确认的残留文件（进入隔离区，可恢复）。",
    "apps.sort.name": "按名称", "apps.sort.size": "按大小", "apps.sort.date": "按安装日期",
    "apps.col.name": "应用", "apps.col.publisher": "发布者", "apps.col.size": "大小", "apps.col.date": "安装日期",
    "apps.loading": "正在读取已安装应用…", "apps.empty": "没有匹配的应用", "apps.store": "Microsoft Store 应用",
    "apps.unknown": "未知", "apps.uninstall.title": "卸载 {0}？", "apps.uninstall.start": "开始卸载",
    "apps.uninstall.msg": "应用大小：{0}\n\n将启动应用自带的卸载程序，请在卸载向导中完成操作。",
    "apps.waiting": "等待「{0}」卸载完成…\n完成卸载向导后会自动继续", "apps.notdetected": "未检测到卸载完成，可能已取消",
    "apps.uninstalled.toast": "「{0}」已卸载", "apps.uninstalled.log": "卸载了应用「{0}」", "apps.uninstall.failed": "无法启动卸载程序：",
    # 残留
    "leftover.title": "发现可能的残留", "leftover.title.f": "「{0}」的卸载残留",
    "leftover.desc": "以下是可确认属于该应用的数据目录与设置键。勾选后进入隔离区（可随时恢复）。",
    "leftover.skip": "不清理，完成", "leftover.clean": "清理勾选项", "leftover.reg": "注册表设置键（导出备份后删除）",
    "leftover.datadir": "数据目录", "leftover.installdir": "安装目录",
    # 工具箱
    "tool.desc": "文件粉碎不会进入回收站；隔离区文件可随时恢复；重复文件基于内容哈希检测。",
    "tool.quarantine.title": "清理隔离区", "tool.quar.empty": "隔离区是空的",
    "tool.quar.summary": "{0} 个项目，共 {1}；超过 {2} 天将自动永久删除",
    "tool.quar.daysleft": "剩 {0} 天", "tool.purge.title": "清空隔离区？",
    "tool.purge.msg": "隔离区中的 {0} 个项目将被永久删除，无法恢复。",
    "tool.shred.title": "文件粉碎", "tool.shred.desc": "从文件系统中永久删除文件，使其不进入回收站。注意：对 SSD 无法保证底层闪存数据立即物理覆写。此操作不可恢复，请谨慎选择文件。",
    "tool.shred.confirm.title": "确认粉碎？", "tool.shred.confirm.msg": "{0}\n\n{1} 将被覆写后永久删除，不进入回收站，无法通过恢复软件轻易还原。\n\n此操作不可恢复！",
    "tool.shred.log": "粉碎了{0}「{1}」", "tool.folder": "文件夹", "tool.file": "文件",
    "tool.dup.title": "重复文件", "tool.dup.desc": "通过内容哈希确认重复（不是只看文件名）。扫描范围默认为图片、视频与下载目录。",
    "tool.dup.none": "没有发现重复文件", "tool.dup.summary": "{0} 组重复文件，可释放 {1}",
    "tool.dup.progress": "已扫描 {0:N0} 个文件…", "tool.dup.more": "… 还有 {0} 组未显示",
    "tool.dup.group": "{0} 个相同文件 · 每个 {1} · 可释放 {2}", "tool.dup.tip": "勾选 = 删除这份副本",
    "tool.dup.clean.title": "删除选中的副本？", "tool.dup.clean.msg": "共 {0} 个文件，{1}，将移入回收站（可还原）。",
    "tool.history.title": "清理历史", "tool.win.title": "Windows 快捷工具", "tool.win.desc": "直达系统自带工具，安全无风险",
    "tool.win.taskmgr": "任务管理器", "tool.win.taskmgr.d": "查看进程与性能",
    "tool.win.storage": "存储设置", "tool.win.storage.d": "管理存储空间",
    "tool.win.diskmgmt": "磁盘管理", "tool.win.diskmgmt.d": "管理分区与磁盘",
    "tool.win.devmgmt": "设备管理器", "tool.win.devmgmt.d": "管理硬件驱动",
    "tool.win.control": "控制面板", "tool.win.control.d": "系统经典设置",
    "tool.win.msinfo": "系统信息", "tool.win.msinfo.d": "查看系统配置",
    "tool.win.update": "Windows 更新", "tool.win.update.d": "检查系统更新",
    "tool.win.restore": "系统还原", "tool.win.restore.d": "创建或使用还原点",
    # 历史
    "history.empty": "还没有活动记录", "history.clear.title": "清空历史？", "history.clear.msg": "所有清理与操作记录将被删除。",
    "history.type.clean": "清理", "history.type.autoclean": "自动清理", "history.type.restore": "恢复",
    "history.type.uninstall": "卸载", "history.type.startup": "启动项", "history.type.quarantine": "隔离区",
    "history.type.shred": "粉碎", "history.type.other": "其他",
    # 设置
    "settings.title": "设置", "settings.sec.general": "通用", "settings.sec.exclude": "排除目录与白名单",
    "settings.sec.notify": "通知", "settings.sec.advanced": "高级", "settings.sec.privacy": "隐私", "settings.sec.about": "关于",
    "settings.exc.desc": "这些目录与文件在所有扫描和清理中都会被跳过。", "settings.exc.empty": "暂无排除目录与白名单",
    "settings.whitelist": "白名单", "settings.excluded": "排除",
    "settings.btn.addexclude": "添加排除目录", "settings.btn.addwhitelist": "添加白名单文件/目录",
    "settings.pick.whitelist": "选择要加入白名单的文件", "settings.pick.exclude": "选择要在扫描清理中排除的目录",
    "settings.days": "天", "settings.threads": "线程",
    "settings.theme.light": "浅色", "settings.theme.dark": "深色", "settings.theme.system": "跟随系统",
    "settings.freq.daily": "每天", "settings.freq.weekly": "每周", "settings.freq.monthly": "每月",
    "settings.lang.auto": "跟随系统语言",
    "settings.row.language": "界面语言", "settings.row.language.d": "切换后立即生效",
    "settings.row.autostart": "开机自动运行清理优化大师", "settings.row.autostart.d": "开机后只在后台托盘待命，不执行任何操作",
    "settings.row.mintotray": "最小化到系统托盘", "settings.row.mintotray.d": "点击最小化按钮时隐藏到托盘",
    "settings.row.closetotray": "关闭按钮最小化到托盘", "settings.row.closetotray.d": "点击关闭按钮时询问并最小化，而非退出",
    "settings.row.theme": "主题",
    "settings.row.quarantine": "清理后进入隔离区", "settings.row.quarantine.d": "非缓存类文件先进入隔离区，可随时恢复（推荐开启）",
    "settings.row.quarantine.days": "隔离区保留时间", "settings.row.quarantine.days.d": "到期后自动永久删除",
    "settings.row.autoclean": "自动清理", "settings.row.autoclean.d": "按计划自动清理临时文件与缓存等安全项目",
    "settings.row.autoclean.freq": "自动清理频率", "settings.row.autoclean.freq.d": "应用运行期间在后台执行",
    "settings.row.notifydisk": "磁盘空间不足提醒", "settings.row.notifydisk.d": "系统盘剩余不足 10% 时提醒（低频，不打扰）",
    "settings.row.notifyauto": "自动清理结果通知", "settings.row.notifyweekly": "每周空间报告",
    "settings.row.notifynews": "产品消息", "settings.row.notifynews.d": "新功能与活动通知，默认关闭",
    "settings.row.threads": "扫描线程数", "settings.row.threads.d": "线程越多扫描越快，但机械硬盘建议调低",
    "settings.row.hidden": "扫描隐藏文件", "settings.row.junction": "跟随目录链接 (Junction)",
    "settings.row.junction.d": "跟随符号链接扫描可能造成重复统计，默认关闭",
    "settings.row.stats": "匿名使用统计", "settings.row.stats.d": "仅上报功能使用次数与错误码，不含任何文件信息；当前版本完全不上传数据",
    "settings.row.crash": "崩溃报告",
    "settings.privacy.desc": "扫描数据全部保存在本地。本应用不收集、不上传任何文件名、路径与内容。",
    "settings.about.slogan": "清得明白，优化得安心。", "settings.about.support": "支持与反馈",
    "settings.about.site": "支持网站", "settings.about.email": "联系邮箱", "settings.about.repo": "开源仓库",
    "settings.about.wechat": "微信扫码或搜索「AI虚拟助理」，获取使用技巧与更新动态。",
    # 向导
    "wizard.welcome": "欢迎使用清理优化大师",
    "wizard.slogan": "清得明白，优化得安心。\n我们只做可解释、可预览、可恢复的清理，不搞虚假加速。",
    "wizard.privacy.title": "隐私承诺",
    "wizard.privacy.items": "· 所有扫描结果、文件名、路径都只保存在你的电脑本地。\n· 默认不开启任何数据上报，也不上传任何文件内容。\n· 关键清理项进入隔离区，7~30 天内可随时恢复。\n· 删除前永远先预览，绝不静默删除你的文件。",
    "wizard.auto.title": "开启自动清理？", "wizard.auto.desc": "仅自动清理系统临时文件与应用缓存等安全项目，每周一次。",
    "wizard.auto.yes": "开启自动清理", "wizard.auto.no": "暂不开启", "wizard.next": "开始体验", "wizard.next2": "下一步",
    # 托盘
    "tray.open": "打开清理优化大师", "tray.quickscan": "快速扫描", "tray.exit": "退出",
    "tray.autoclean": "自动清理：", "tray.on": "开", "tray.off": "关",
    # 对话/提示
    "dlg.exit.title": "退出清理优化大师？", "dlg.exit.msg": "退出后将不再提供自动清理与磁盘空间监控。",
    "toast.traymin": "已最小化到托盘，右键托盘图标可退出",
    "toast.scanstopped": "已停止扫描", "toast.scanfailed": "扫描失败：", "toast.noselect": "请先勾选要清理的项目",
    "toast.cleancancelled": "清理已取消", "toast.cleanfailed": "清理失败：", "toast.nothing": "没有发现可清理的内容",
    "toast.cleaned": "已清理，释放了 {0}", "toast.readfailed": "读取失败：", "toast.langapplied": "界面语言已切换",
}

# ============ 组装与生成 ============
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lang_data_en import EN  # noqa: E402
from lang_data_rules import RULES  # noqa: E402
import lang_data_tw, lang_data_jako, lang_data_eu  # noqa: E402

def merge_over(base, over):
    m = dict(base)
    m.update(over or {})
    return m

# 规则元数据并入 zh-CN 与 en-US 基础字典
for rid, zn, zr, zi, en_n, en_r, en_i in RULES:
    ZH[f"rule.{rid}.name"] = zn
    ZH[f"rule.{rid}.reason"] = zr
    ZH[f"rule.{rid}.impact"] = zi
    EN[f"rule.{rid}.name"] = en_n
    EN[f"rule.{rid}.reason"] = en_r
    EN[f"rule.{rid}.impact"] = en_i

LANGS = {
    "zh-CN": ZH,
    "zh-TW": merge_over(ZH, lang_data_tw.ZH_TW),
    "en-US": merge_over(ZH, EN),
    "ja-JP": merge_over(EN, lang_data_jako.JA),
    "ko-KR": merge_over(EN, lang_data_jako.KO),
    "fr-FR": merge_over(EN, lang_data_eu.FR),
    "es-ES": merge_over(EN, lang_data_eu.ES),
    "de-DE": merge_over(EN, lang_data_eu.DE),
}
# zh-TW 规则名：从 zh-CN 做常用字转换
TW_PHRASES = [("浏览器", "瀏覽器"), ("缓存", "快取"), ("视频", "影片"), ("文件", "檔案"),
              ("日志", "日誌"), ("磁盘", "磁碟"), ("网络", "網路"), ("软件", "軟體"), ("优化", "優化")]
TW_MAP = str.maketrans("优击运览历记别库图转驱权户删签器态忆载录执", "優擊運覽歷記別庫圖轉驅權戶刪簽器態憶載錄執")

def to_tw(s):
    out = s
    for zh, tw in TW_PHRASES:
        out = out.replace(zh, tw)
    return out.translate(TW_MAP)

for rid, zn, zr, zi, en_n, en_r, en_i in RULES:
    LANGS["zh-TW"][f"rule.{rid}.name"] = to_tw(zn)
    LANGS["zh-TW"][f"rule.{rid}.reason"] = to_tw(zr)
    LANGS["zh-TW"][f"rule.{rid}.impact"] = to_tw(zi)

def esc(s):
    return s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;").replace('"', "&quot;")

os.makedirs(OUT, exist_ok=True)
master = set(ZH.keys()) | {f"rule.{r[0]}.{f}" for r in RULES for f in ("name", "reason", "impact")}
ok = True
for code, d in LANGS.items():
    missing = master - set(d.keys())
    if missing:
        print(f"[{code}] 缺 {len(missing)} 个 key（回退链处理）:", sorted(missing)[:5], "...")
    lines = ['<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"',
             '                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"',
             '                    xmlns:sys="clr-namespace:System;assembly=mscorlib">']
    for k in sorted(d.keys()):
        lines.append(f'    <sys:String x:Key="{k}">{esc(d[k])}</sys:String>')
    lines.append("</ResourceDictionary>")
    fp = os.path.join(OUT, f"{code}.xaml")
    with io.open(fp, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    print(f"[{code}] {len(d)} keys -> {fp}")
print("master keys:", len(master))
