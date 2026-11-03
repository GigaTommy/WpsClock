#include <QtWidgets>
#include <QSoundEffect>
#include <QMediaPlayer>
#include <sys/file.h>
#include <sys/stat.h>
#include <fcntl.h>
#include <unistd.h>
#include "appdata.h"
#include "x11probe.h"

using namespace wpsclock;
static const char *VERSION="2.5.0-kylin.1-rc1";

class Audio : public QObject {
    QSoundEffect effect;
    QMediaPlayer media;
    QString folder;
public:
    std::function<void(QString)> failed;
    explicit Audio(QString path):folder(std::move(path)) {
        connect(&effect,&QSoundEffect::statusChanged,this,[this]{if(effect.status()==QSoundEffect::Error&&failed)failed(ui("提示音播放失败；请检查音频设备或选择 PCM WAV 文件。 "));});
        connect(&media,QOverload<QMediaPlayer::Error>::of(&QMediaPlayer::error),this,[this](QMediaPlayer::Error){if(failed)failed(ui("自选声音播放失败：")+media.errorString());});
        effect.setVolume(.8);media.setVolume(80);
    }
    void stop(){effect.stop();media.stop();}
    void play(bool final,const QString &custom){
        stop();QString path=custom;
        if(path.isEmpty()){
            path=folder+(final?"/end.wav":"/near.wav");
            if(!QFileInfo::exists(path)){QSaveFile f(path);QByteArray data=wave(final);if(!f.open(QIODevice::WriteOnly)||f.write(data)!=data.size()||!f.commit()){if(failed)failed(ui("无法生成内置提示音，请检查配置目录权限。"));return;}}
        }
        if(!QFileInfo(path).isFile()){if(failed)failed(ui("声音文件不存在，请重新选择。"));return;}
        if(path.endsWith(".wav",Qt::CaseInsensitive)){effect.setSource(QUrl::fromLocalFile(QFileInfo(path).absoluteFilePath()));effect.play();}
        else {media.setMedia(QUrl::fromLocalFile(QFileInfo(path).absoluteFilePath()));media.play();}
    }
};

class SettingsDialog : public QDialog {
    Config draft;
    QTimeEdit *duration,*warning;
    QSpinBox *grace,*width,*height,*font,*opacity;
    QCheckBox *automatic,*countUp,*stopAtZero,*sync,*hideIdle,*nearSound,*endSound,*clockTime;
    QComboBox *pauseKey,*resetKey,*language;
    QLineEdit *nearFile,*endFile,*prefix;
    QTableWidget *rules;
    QPlainTextEdit *diagnostics;
    QLabel *live;
    std::function<QString()> report;
    static QTimeEdit *timeInput(int seconds,QWidget *parent){auto *w=new QTimeEdit(QTime(0,0).addSecs(seconds),parent);w->setDisplayFormat("HH:mm:ss");w->setMinimumWidth(135);return w;}
    static int seconds(QTimeEdit *w){return QTime(0,0).secsTo(w->time());}
    static QSpinBox *number(int value,int lo,int hi,QWidget *parent){auto *w=new QSpinBox(parent);w->setRange(lo,hi);w->setValue(value);return w;}
    static QCheckBox *check(const QString &label,bool value,QWidget *parent){auto *w=new QCheckBox(label,parent);w->setChecked(value);return w;}
    void addRule(const Rule &r){
        int row=rules->rowCount();rules->insertRow(row);rules->setItem(row,0,new QTableWidgetItem(r.file));
        auto *t=timeInput(r.seconds,rules);rules->setCellWidget(row,1,t);auto *bg=new QCheckBox(ui("会议背景"),rules);bg->setChecked(r.background);rules->setCellWidget(row,2,bg);
        t->setEnabled(!r.background);connect(bg,&QCheckBox::toggled,t,&QWidget::setDisabled);
    }
    QPushButton *colorButton(const QString &label,QColor *value,QWidget *parent){
        auto *b=new QPushButton(label,parent);auto update=[b,value]{b->setStyleSheet("QPushButton {border:2px solid "+value->name()+";padding:6px;}");};update();
        connect(b,&QPushButton::clicked,this,[this,value,update]{QColor c=QColorDialog::getColor(*value,this,ui("选择颜色"));if(c.isValid()){*value=c;update();}});return b;
    }
public:
    explicit SettingsDialog(const Config &config,Audio &audio,std::function<QString()> diagnostic,QWidget *parent=nullptr):QDialog(parent),draft(config),report(std::move(diagnostic)) {
        setWindowFlag(Qt::WindowStaysOnTopHint);
        setWindowTitle(QString(ui("WpsClock 麒麟版 · 设置 (%1)")).arg(VERSION));resize(780,610);setMinimumSize(640,480);
        auto *layout=new QVBoxLayout(this);auto *tabs=new QTabWidget(this);layout->addWidget(tabs);
        auto *timing=new QWidget(tabs);auto *tl=new QVBoxLayout(timing);auto *tf=new QFormLayout;tl->addLayout(tf);
        duration=timeInput(config.duration,timing);tf->addRow(ui("默认时长（0 为不限时）"),duration);
        automatic=check(ui("自动识别 WPS 放映"),config.automatic,timing);tf->addRow(automatic);
        live=new QLabel(timing);live->setWordWrap(true);live->setTextInteractionFlags(Qt::TextSelectableByMouse);tl->addWidget(live);
        auto *tip=new QLabel(ui("文件规则：可填写文件名或完整路径。同名文件无法确定时使用默认时长；会议背景用于暂停并隐藏计时。"),timing);tip->setWordWrap(true);tl->addWidget(tip);
        rules=new QTableWidget(0,3,timing);rules->setHorizontalHeaderLabels({ui("PPT / PDF 文件"),ui("时长"),ui("用途")});rules->horizontalHeader()->setSectionResizeMode(0,QHeaderView::Stretch);rules->setColumnWidth(1,150);rules->setColumnWidth(2,110);rules->setSelectionBehavior(QAbstractItemView::SelectRows);tl->addWidget(rules,1);
        for(const auto &r:config.rules)addRule(r);
        auto *ruleButtons=new QHBoxLayout;tl->addLayout(ruleButtons);auto *browse=new QPushButton(ui("选择文件…"),timing);auto *add=new QPushButton(ui("手工添加"),timing);auto *remove=new QPushButton(ui("删除所选"),timing);ruleButtons->addWidget(browse);ruleButtons->addWidget(add);ruleButtons->addWidget(remove);ruleButtons->addStretch();
        connect(browse,&QPushButton::clicked,this,[this]{auto paths=QFileDialog::getOpenFileNames(this,ui("选择演示文件"),QString(),ui("演示文件 (*.ppt *.pptx *.pps *.ppsx *.pdf)"));for(const auto &p:paths){Rule r;r.file=p;r.seconds=seconds(duration);addRule(r);}});
        connect(add,&QPushButton::clicked,this,[this]{Rule r;r.file=ui("示例发言.pptx");r.seconds=seconds(duration);addRule(r);rules->editItem(rules->item(rules->rowCount()-1,0));});
        connect(remove,&QPushButton::clicked,this,[this]{QSet<int> selected;for(auto *item:rules->selectedItems())selected.insert(item->row());auto rows=selected.values();std::sort(rows.begin(),rows.end(),std::greater<int>());for(int row:rows)rules->removeRow(row);});
        tabs->addTab(timing,ui("时长与文件"));
        auto *control=new QWidget(tabs);auto *cf=new QFormLayout(control);
        countUp=check(ui("正计时（仍按设定时长提醒）"),config.countUp,control);stopAtZero=check(ui("时间到后停止计时"),config.stopAtZero,control);cf->addRow(countUp);cf->addRow(stopAtZero);
        warning=timeInput(config.warning,control);cf->addRow(ui("提前提醒（0 为关闭）"),warning);
        grace=number(config.grace,0,120,control);grace->setSuffix(ui(" 秒"));cf->addRow(ui("退出放映宽限"),grace);
        auto soundRow=[&](const QString &label,bool enabled,const QString &path,QCheckBox *&toggle,QLineEdit *&field,bool final){
            auto *box=new QWidget(control);auto *line=new QHBoxLayout(box);line->setContentsMargins(0,0,0,0);toggle=check(label,enabled,box);field=new QLineEdit(path,box);field->setPlaceholderText(ui("留空使用内置提示音"));auto *choose=new QPushButton(ui("浏览"),box);auto *test=new QPushButton(ui("试听"),box);line->addWidget(toggle);line->addWidget(field,1);line->addWidget(choose);line->addWidget(test);cf->addRow(box);
            QLineEdit *f=field;connect(choose,&QPushButton::clicked,this,[this,f]{QString p=QFileDialog::getOpenFileName(this,ui("选择声音"),QString(),ui("声音文件 (*.wav *.mp3)"));if(!p.isEmpty())f->setText(p);});connect(test,&QPushButton::clicked,this,[&audio,f,final]{audio.play(final,f->text().trimmed());});
        };
        soundRow(ui("提前声音"),config.nearSound,config.nearFile,nearSound,nearFile,false);soundRow(ui("到时声音"),config.endSound,config.endFile,endSound,endFile,true);
        pauseKey=new QComboBox(control);resetKey=new QComboBox(control);for(int i=1;i<=12;++i){pauseKey->addItem(QString("F%1").arg(i),i);resetKey->addItem(QString("F%1").arg(i),i);}pauseKey->setCurrentIndex(config.pauseKey-1);resetKey->setCurrentIndex(config.resetKey-1);cf->addRow(ui("全局开始 / 暂停 / 继续"),pauseKey);cf->addRow(ui("全局重新计时"),resetKey);
        auto *boundary=new QLabel(ui("到时保持放映，不发送 Esc。页码和自动单页计时在本版不可用。MP3 播放取决于本机解码组件，建议使用 WAV。"),control);boundary->setWordWrap(true);cf->addRow(boundary);tabs->addTab(control,ui("控制与提醒"));
        auto *appearance=new QWidget(tabs);auto *af=new QFormLayout(appearance);
        width=number(config.width,140,1600,appearance);height=number(config.height,70,900,appearance);font=number(config.fontSize,16,120,appearance);opacity=number(config.opacity,0,100,appearance);opacity->setSuffix(" %");
        af->addRow(ui("窗口宽度"),width);af->addRow(ui("窗口高度"),height);af->addRow(ui("数字字号"),font);af->addRow(ui("背景不透明度（数字保持清晰）"),opacity);
        auto *colors=new QWidget(appearance);auto *cl=new QHBoxLayout(colors);cl->setContentsMargins(0,0,0,0);cl->addWidget(colorButton(ui("正常文字"),&draft.foreground,colors));cl->addWidget(colorButton(ui("正常背景"),&draft.background,colors));cl->addWidget(colorButton(ui("提醒文字"),&draft.warningForeground,colors));cl->addWidget(colorButton(ui("提醒背景"),&draft.warningBackground,colors));af->addRow(colors);
        prefix=new QLineEdit(config.prefix,appearance);prefix->setMaxLength(20);af->addRow(ui("超时前缀"),prefix);
        sync=check(ui("在各屏幕同步显示"),config.syncScreens,appearance);hideIdle=check(ui("未计时时隐藏悬浮窗"),config.hideIdle,appearance);clockTime=check(ui("显示当前机器时间"),config.clockTime,appearance);af->addRow(sync);af->addRow(hideIdle);af->addRow(clockTime);
        auto *help=new QLabel(ui("拖动数字区域移动位置，拖动右下角调整尺寸。锁定 / 点击穿透由托盘菜单控制；托盘不可用时自动解除锁定。"),appearance);help->setWordWrap(true);af->addRow(help);tabs->addTab(appearance,ui("外观"));
        auto *about=new QWidget(tabs);auto *al=new QVBoxLayout(about);auto *intro=new QLabel(QString(ui("WpsClock 麒麟绿色版 %1\n离线放映计时，无需安装或管理员权限。\n预先设置文件时长，切换放映自动切换计时。诊断隐藏文件名及路径。")).arg(VERSION),about);intro->setWordWrap(true);al->addWidget(intro);
        language=new QComboBox(about);language->addItem(ui("跟随系统 / System"),"system");language->addItem(ui("简体中文"),"zh-CN");language->addItem("English","en");language->setCurrentIndex(qMax(0,language->findData(config.language)));
        auto *languageRow=new QFormLayout;languageRow->addRow(ui("语言 / Language"),language);al->addLayout(languageRow);
        diagnostics=new QPlainTextEdit(about);diagnostics->setReadOnly(true);diagnostics->setPlainText(report());al->addWidget(diagnostics,1);auto *diagnosticButtons=new QHBoxLayout;al->addLayout(diagnosticButtons);auto *refresh=new QPushButton(ui("刷新诊断"),about);auto *copy=new QPushButton(ui("复制诊断（隐藏文件名）"),about);diagnosticButtons->addWidget(refresh);diagnosticButtons->addWidget(copy);connect(refresh,&QPushButton::clicked,this,[this]{diagnostics->setPlainText(report());});connect(copy,&QPushButton::clicked,this,[this]{QApplication::clipboard()->setText(report());});tabs->addTab(about,ui("版本与检测"));
        auto *bottom=new QDialogButtonBox(QDialogButtonBox::Save|QDialogButtonBox::Cancel,this);bottom->button(QDialogButtonBox::Save)->setText(ui("保存"));bottom->button(QDialogButtonBox::Cancel)->setText(ui("取消"));layout->addWidget(bottom);
        connect(bottom,&QDialogButtonBox::rejected,this,&QDialog::reject);connect(bottom,&QDialogButtonBox::accepted,this,[this]{
            if(pauseKey->currentIndex()==resetKey->currentIndex()){QMessageBox::warning(this,ui("请检查快捷键"),ui("暂停与重置不能使用同一个按键。"));return;}
            QVector<Rule> parsed;QSet<QString> names;
            for(int row=0;row<rules->rowCount();++row){Rule r;r.file=rules->item(row,0)?rules->item(row,0)->text().trimmed():QString();
                if(r.file.isEmpty()||r.file.contains('\n')||names.contains(normalized(r.file))){QMessageBox::warning(this,ui("请检查文件规则"),QString(ui("第 %1 行文件名为空、包含换行或重复。")).arg(row+1));return;}
                names.insert(normalized(r.file));r.seconds=seconds(qobject_cast<QTimeEdit*>(rules->cellWidget(row,1)));r.background=qobject_cast<QCheckBox*>(rules->cellWidget(row,2))->isChecked();parsed.append(r);
            }
            draft.language=language->currentData().toString();draft.rules=parsed;draft.duration=seconds(duration);draft.warning=seconds(warning);draft.grace=grace->value();draft.automatic=automatic->isChecked();draft.countUp=countUp->isChecked();draft.stopAtZero=stopAtZero->isChecked();draft.syncScreens=sync->isChecked();draft.hideIdle=hideIdle->isChecked();draft.clockTime=clockTime->isChecked();
            draft.width=width->value();draft.height=height->value();draft.fontSize=font->value();draft.opacity=opacity->value();draft.prefix=prefix->text();draft.pauseKey=pauseKey->currentIndex()+1;draft.resetKey=resetKey->currentIndex()+1;draft.nearSound=nearSound->isChecked();draft.endSound=endSound->isChecked();draft.nearFile=nearFile->text().trimmed();draft.endFile=endFile->text().trimmed();
            accept();
        });
    }
    Config value() const{return draft;}
    void setStatus(const QString &s){live->setText(s);}
};

class Overlay : public QWidget {
    QPoint anchor;QRect initial;bool dragging=false,resizing=false;
public:
    QString screenKey,number="08:00",status=ui("待命 · 右键设置");
    Config config;bool warn=false,paused=false,composite=true;
    std::function<void(QPoint)> menu;
    std::function<void()> settings;
    std::function<void(Overlay*,bool)> moved;
    explicit Overlay(QString key):screenKey(std::move(key)){
        setWindowFlags(Qt::Tool|Qt::FramelessWindowHint|Qt::WindowStaysOnTopHint|Qt::WindowDoesNotAcceptFocus);
        setAttribute(Qt::WA_ShowWithoutActivating);setAttribute(Qt::WA_TranslucentBackground);setFocusPolicy(Qt::NoFocus);setMouseTracking(true);
        setWindowTitle(ui("WpsClock 麒麟倒计时"));setAccessibleName(ui("演讲计时器"));
    }
protected:
    void paintEvent(QPaintEvent*) override {
        QPainter p(this);p.setRenderHint(QPainter::Antialiasing);QColor bg=warn?config.warningBackground:config.background;
        bg.setAlpha(composite?qRound(config.opacity*2.55):255);p.setPen(Qt::NoPen);p.setBrush(bg);p.drawRoundedRect(rect(),10,10);
        if(warn){QPen pen(config.warningForeground);pen.setWidth(3);p.setPen(pen);p.setBrush(Qt::NoBrush);p.drawRoundedRect(rect().adjusted(2,2,-2,-2),8,8);}
        QFont f("DejaVu Sans Mono",config.fontSize,QFont::DemiBold);QRect area=rect().adjusted(10,5,-10,-28);
        while(f.pointSize()>12&&(QFontMetrics(f).horizontalAdvance(number)>area.width()||QFontMetrics(f).height()>area.height()))f.setPointSize(f.pointSize()-1);
        p.setFont(f);p.setPen(warn?config.warningForeground:config.foreground);p.drawText(area,Qt::AlignCenter,number);
        f=QApplication::font();f.setPointSize(10);p.setFont(f);QString s=paused?ui("已暂停 · ")+status:status;
        p.drawText(rect().adjusted(9,height()-27,-9,-5),Qt::AlignCenter,QFontMetrics(f).elidedText(s,Qt::ElideRight,qMax(0,width()-18)));
        if(!config.locked){p.setPen(QPen(warn?config.warningForeground:config.foreground,1));p.drawLine(width()-12,height()-5,width()-5,height()-12);p.drawLine(width()-7,height()-5,width()-5,height()-7);}
    }
    void contextMenuEvent(QContextMenuEvent *e) override {if(menu)menu(e->globalPos());}
    void mouseDoubleClickEvent(QMouseEvent *e) override {if(e->button()==Qt::LeftButton&&settings)settings();}
    void mousePressEvent(QMouseEvent *e) override {
        if(config.locked||e->button()!=Qt::LeftButton)return;
        anchor=e->globalPos();initial=geometry();resizing=e->pos().x()>width()-18&&e->pos().y()>height()-18;dragging=!resizing;
    }
    void mouseMoveEvent(QMouseEvent *e) override {
        if(config.locked)return;
        if(dragging)move(initial.topLeft()+e->globalPos()-anchor);
        else if(resizing){auto d=e->globalPos()-anchor;resize(qBound(140,initial.width()+d.x(),1600),qBound(70,initial.height()+d.y(),900));}
        else setCursor(e->pos().x()>width()-18&&e->pos().y()>height()-18?Qt::SizeFDiagCursor:Qt::ArrowCursor);
    }
    void mouseReleaseEvent(QMouseEvent *e) override {if(e->button()==Qt::LeftButton&&(dragging||resizing)){bool sized=resizing;dragging=resizing=false;if(moved)moved(this,sized);}}
};

class Controller : public QObject {
    Config config;
    QString configPath,issue;
    QElapsedTimer monotonic;
    QTimer timer;
    Session session;
    X11Probe probe;
    Audio audio;
    QSystemTrayIcon tray;
    QMenu trayMenu;
    QVector<Overlay*> panels;
    QPointer<SettingsDialog> dialog;
    QVector<WindowInfo> windows;
    unsigned long selected=0;
    QRect showBounds;
    double lastScan=-1,lastRaise=-1;
    bool storageValid=true,preview=false;
    double previewAt=0;
    QString matchStatus=ui("等待 WPS 放映"),notice;
    double now() const{return monotonic.elapsed()/1000.0;}
    int resetSeconds() const {return session.file.empty()?config.duration:matchTitle(config,QString(),text(session.file)).seconds;}
    QString screenKey(QScreen *s) const {return s->name();}
    void notify(const QString &message){notice=message;if(tray.isVisible())tray.showMessage("WpsClock",message,QSystemTrayIcon::Warning,8000);}
    bool save(bool explicitSave=false){
        if(!storageValid&&!explicitSave)return false;
        QString error;if(!config.save(configPath,error)){issue=ui("设置保存失败：")+error;notify(issue);return false;}
        storageValid=true;issue.clear();return true;
    }
    void place(Overlay *p,QScreen *s){
        QRect a=s->availableGeometry();p->resize(qMin(config.width,a.width()),qMin(config.height,a.height()));
        auto pos=config.positions.value(p->screenKey).toObject();QPoint point(a.right()-p->width()-15,a.top()+16);
        if(pos.contains("x")&&pos.contains("y"))point=QPoint(pos["x"].toInt(),pos["y"].toInt());
        p->move(qBound(a.left(),point.x(),qMax(a.left(),a.right()-p->width()+1)),qBound(a.top(),point.y(),qMax(a.top(),a.bottom()-p->height()+1)));
    }
    void buildPanels(){
        qDeleteAll(panels);panels.clear();
        auto screens=QGuiApplication::screens();if(screens.isEmpty())return;
        if(!config.syncScreens){QScreen *chosen=QGuiApplication::primaryScreen();for(auto *s:screens)if(s->geometry().contains(showBounds.center()))chosen=s;screens={chosen};}
        for(auto *s:screens){auto *p=new Overlay(screenKey(s));p->config=config;p->composite=probe.composite();place(p,s);
            p->menu=[this](QPoint point){popup(point);};p->settings=[this]{openSettings();};
            p->moved=[this](Overlay *panel,bool sized){
                QScreen *screen=nullptr;for(auto *s:QGuiApplication::screens())if(screenKey(s)==panel->screenKey)screen=s;
                if(!screen)screen=QGuiApplication::primaryScreen();
                if(sized){config.width=panel->width();config.height=panel->height();}
                auto a=screen->availableGeometry();QPoint point(qBound(a.left(),panel->x(),qMax(a.left(),a.right()-panel->width()+1)),qBound(a.top(),panel->y(),qMax(a.top(),a.bottom()-panel->height()+1)));
                QJsonObject pos;pos["x"]=point.x();pos["y"]=point.y();config.positions[panel->screenKey]=pos;panel->move(point);
                if(sized)for(auto *p:panels){p->config=config;QScreen *sc=nullptr;for(auto *candidate:QGuiApplication::screens())if(screenKey(candidate)==p->screenKey)sc=candidate;if(sc)place(p,sc);}
                save();
            };
            panels.append(p);
        }
        updatePanels();
    }
    void fillMenu(QMenu &menu){
        auto *toggle=menu.addAction(session.clock.active?(session.clock.paused?ui("继续计时"):ui("暂停计时")):ui("开始计时"),this,[this]{preview=false;session.toggle(now(),config.duration);});
        toggle->setEnabled(!(config.stopAtZero&&session.clock.ended));
        menu.addAction(ui("重新计时"),this,[this]{preview=false;audio.stop();session.reset(now(),resetSeconds());});
        menu.addAction(ui("结束本场"),this,[this]{preview=false;audio.stop();session.stop();});
        menu.addSeparator();auto *autoAction=menu.addAction(ui("自动识别放映"));autoAction->setCheckable(true);autoAction->setChecked(config.automatic);
        connect(autoAction,&QAction::triggered,this,[this](bool checked){config.automatic=checked;session.monitoring=checked;save();});
        auto *lock=menu.addAction(ui("锁定位置 / 点击穿透"));lock->setCheckable(true);lock->setChecked(config.locked);lock->setEnabled(QSystemTrayIcon::isSystemTrayAvailable());
        connect(lock,&QAction::triggered,this,[this](bool checked){config.locked=checked;save();for(auto *p:panels){p->config=config;probe.top(p->winId(),checked);}});
        auto *choose=menu.addMenu(ui("本场手动指定时长（保留已用时间）"));choose->setEnabled(session.clock.active&&!session.held);
        for(const auto &r:config.rules)if(!r.background)choose->addAction(QFileInfo(r.file).fileName()+" · "+text(Clock::format(r.seconds)),this,[this,r]{session.choose(now(),r.seconds);matchStatus=ui("本场手动指定：")+QFileInfo(r.file).fileName();});
        menu.addAction(ui("恢复右上角位置"),this,[this]{config.positions={};save();QTimer::singleShot(0,this,[this]{buildPanels();});});
        menu.addAction(ui("本地设置…"),this,[this]{QTimer::singleShot(0,this,[this]{openSettings();});});
        menu.addSeparator();menu.addAction(ui("退出"),qApp,&QApplication::quit);
    }
    void popup(QPoint point){QMenu menu;fillMenu(menu);menu.exec(point);}
    QString diagnostic() const {
        QStringList lines;lines<<QString(ui("版本：%1")).arg(VERSION)<<ui("系统：")+QSysInfo::prettyProductName()<<ui("架构：")+QSysInfo::currentCpuArchitecture()<<ui("Qt 运行库：")+QString(qVersion())<<ui("Qt 平台：")+QGuiApplication::platformName()<<ui("X11 连接：")+(probe.available()?QString(ui("可用")):QString(ui("不可用")))<<ui("窗口合成：")+(probe.composite()?QString(ui("可用")):QString(ui("不可用，使用不透明背景")));
        lines<<QString(ui("WPS 候选窗口：%1")).arg(windows.size());int shows=0;for(const auto &w:windows)if(w.show)++shows;
        lines<<QString(ui("确认放映窗口：%1；已选择：%2")).arg(shows).arg(selected?ui("是"):ui("否"));
        for(const auto &w:windows)lines<<QString(ui("窗口特征：%1；%2；文件信息=%3；规则命中=%4；歧义=%5")).arg(w.klass,ui(w.reason),w.file.isEmpty()?ui("无"):ui("有"),w.match.matched?ui("是"):ui("否"),w.match.ambiguous?ui("是"):ui("否"));
        lines<<QString(ui("自动监测：%1；手动接管：%2；会议背景暂停：%3")).arg(session.monitoring?ui("开"):ui("关"),session.manual?ui("是"):ui("否"),session.held?ui("是"):ui("否"));
        lines<<ui("快捷键：")+(probe.warning.isEmpty()?QString(ui("注册成功")):probe.warning)<<ui("配置状态：")+(storageValid?QString(ui("可用")):QString(ui("读取异常，原文件保留")));
        lines<<ui("限制：本版无页码/单页计时/自动退出放映；尚需目标麒麟 WPS 实机验收。");
        return lines.join('\n');
    }
    void scan(){
        QList<QRect> screens;for(auto *s:QGuiApplication::screens())screens<<s->geometry();
        windows=probe.scan(config,screens);int index=selectShow(windows,selected);Observation o;
        if(index>=0){const auto &w=windows[index];selected=w.id;QRect old=showBounds;showBounds=w.bounds;o.present=true;o.window=std::to_string(w.id);o.file=bytes(w.file);o.seconds=w.match.seconds;o.background=w.match.background;o.matched=w.match.matched;o.ambiguous=w.match.ambiguous;
            if(!session.manualDuration)matchStatus=ui(w.match.explanation);
            if(!config.syncScreens&&old!=showBounds)QTimer::singleShot(0,this,[this]{buildPanels();});
        }else {selected=0;if(!session.clock.active)matchStatus=ui("等待 WPS 放映");}
        if(!preview){
            bool wasHeld=session.held;
            session.observe(now(),o);
            if(!wasHeld&&session.held)audio.stop();
        }
    }
    void updatePanels(){
        bool trayAvailable=QSystemTrayIcon::isSystemTrayAvailable();
        if(!trayAvailable&&config.locked){config.locked=false;for(auto *p:panels){p->config=config;probe.top(p->winId(),false);}notify(ui("托盘不可用，已解除点击穿透。"));}
        bool hidden=(session.held&&session.monitoring)||(config.hideIdle&&!session.clock.active&&trayAvailable);
        for(auto *p:panels){p->config=config;p->warn=session.clock.warned||session.clock.ended;p->paused=session.clock.paused;
            p->number=session.clock.active?text(session.clock.display(now(),config.countUp,bytes(config.prefix))):text(Clock::format(config.duration));
            if(!probe.warning.isEmpty())p->status=ui("快捷键不可用 · 右键控制");
            else if(!notice.isEmpty())p->status=ui("有提示 · 双击查看设置");
            else if(session.held)p->status=ui("会议背景暂停");
            else if(!session.clock.active)p->status=ui("待命 · F")+QString::number(config.pauseKey)+ui(" 开始");
            else if(config.clockTime)p->status=QTime::currentTime().toString("HH:mm:ss");
            else p->status=session.manual?ui("手动计时"):(session.manualDuration?ui("本场已指定时长"):matchStatus);
            p->setToolTip(notice.isEmpty()?p->status:notice);p->setVisible(!hidden);p->update();
        }
        if(dialog){QString s=ui("当前：")+(session.file.empty()?QString(ui("未识别文件")):text(session.file))+"\n"+matchStatus;if(!issue.isEmpty())s+="\n"+issue;if(!notice.isEmpty())s+="\n"+notice;dialog->setStatus(s);}
    }
    void tick(){
        probe.drain([this](int action){preview=false;if(action==1){if(!(config.stopAtZero&&session.clock.ended))session.toggle(now(),config.duration);}else{audio.stop();session.reset(now(),resetSeconds());}});
        if(now()-lastScan>=.4){lastScan=now();scan();}
        int signal=session.clock.advance(now(),preview?4:config.warning,config.stopAtZero);
        if(signal){bool final=signal==2;if(final?config.endSound:config.nearSound)audio.play(final,final?config.endFile:config.nearFile);}
        if(preview&&now()-previewAt>=11){preview=false;session.stop();audio.stop();}
        updatePanels();
        if(now()-lastRaise>=1){lastRaise=now();if(!dialog&&!QApplication::activePopupWidget())for(auto *p:panels)if(p->isVisible())probe.top(p->winId(),config.locked);}
    }
public:
    explicit Controller(const QString &folder):configPath(folder+"/settings.json"),audio(folder){
        config=Config::load(configPath,issue);UiText::useLanguage(config.language);storageValid=issue.isEmpty();monotonic.start();session.monitoring=config.automatic;session.grace=config.grace;
        audio.failed=[this](QString s){notify(s);};
        QPixmap icon(64,64);icon.fill(Qt::transparent);{QPainter p(&icon);p.setRenderHint(QPainter::Antialiasing);p.setBrush(QColor("#0078d7"));p.setPen(Qt::NoPen);p.drawRoundedRect(1,1,62,62,14,14);p.setPen(Qt::white);p.setFont(QFont("Sans",24,QFont::Bold));p.drawText(icon.rect(),Qt::AlignCenter,ui("计"));}
        qApp->setWindowIcon(QIcon(icon));tray.setIcon(QIcon(icon));tray.setToolTip(ui("WpsClock 麒麟演讲计时器"));tray.setContextMenu(&trayMenu);tray.show();
        connect(&trayMenu,&QMenu::aboutToShow,this,[this]{trayMenu.clear();fillMenu(trayMenu);});
        connect(&tray,&QSystemTrayIcon::activated,this,[this](QSystemTrayIcon::ActivationReason reason){if(reason==QSystemTrayIcon::DoubleClick)openSettings();});
        probe.hotkeys(config.pauseKey,config.resetKey);buildPanels();
        connect(qApp,&QGuiApplication::screenAdded,this,[this](QScreen *s){connect(s,&QScreen::geometryChanged,this,[this]{buildPanels();});buildPanels();});
        connect(qApp,&QGuiApplication::screenRemoved,this,[this](QScreen*){QTimer::singleShot(0,this,[this]{buildPanels();});});
        for(auto *s:QGuiApplication::screens())connect(s,&QScreen::geometryChanged,this,[this]{buildPanels();});
        connect(&timer,&QTimer::timeout,this,[this]{tick();});timer.start(50);
        if(!storageValid||!QFileInfo::exists(configPath))QTimer::singleShot(250,this,[this]{openSettings();});
        if(!issue.isEmpty())notify(issue);
        if(!probe.composite())notify(ui("桌面合成不可用，已使用不透明背景。"));
    }
    ~Controller(){audio.stop();qDeleteAll(panels);}
    void openSettings(){
        if(dialog){dialog->show();dialog->raise();dialog->activateWindow();return;}
        SettingsDialog settings(config,audio,[this]{return diagnostic();});dialog=&settings;
        if(auto *s=QGuiApplication::primaryScreen()){auto a=s->availableGeometry();settings.resize(qMin(780,a.width()-30),qMin(610,a.height()-30));}
        updatePanels();int result=settings.exec();dialog=nullptr;audio.stop();
        if(result==QDialog::Accepted){Config old=config;Config next=settings.value();
            if(!storageValid&&QMessageBox::question(nullptr,ui("覆盖损坏的设置？"),ui("原设置读取失败。保存将替换原设置文件，是否继续？"))!=QMessageBox::Yes)return;
            config=next;if(!save(true)){config=old;QMessageBox::warning(nullptr,ui("设置未保存"),issue);return;}
            UiText::useLanguage(config.language);tray.setToolTip(ui("WpsClock 麒麟演讲计时器"));notice.clear();session.monitoring=config.automatic;session.grace=config.grace;probe.hotkeys(config.pauseKey,config.resetKey);buildPanels();
        }
    }
    void demo(){session.startManual(now(),8);preview=true;previewAt=now();}
    void screenshot(const QString &path){if(!panels.isEmpty())panels.first()->grab().save(path);}
    void settingsScreenshot(const QString &path,int tab){if(dialog){auto *tabs=dialog->findChild<QTabWidget*>();if(tabs)tabs->setCurrentIndex(qBound(0,tab,3));QApplication::processEvents();dialog->grab().save(path);}}
};

int main(int argc,char **argv){
    // Match the target X11 environment; do not change the system login/session.
    if(qEnvironmentVariableIsEmpty("QT_QPA_PLATFORM")&&!qEnvironmentVariableIsEmpty("DISPLAY"))qputenv("QT_QPA_PLATFORM","xcb");
    QApplication::setAttribute(Qt::AA_DisableHighDpiScaling);
    QApplication app(argc,argv);QApplication::setQuitOnLastWindowClosed(false);
    QCoreApplication::setApplicationName("WpsClock-Kylin");QCoreApplication::setApplicationVersion(VERSION);QCoreApplication::setOrganizationName("WpsClock");
    QStringList args=app.arguments();if(args.contains("--version")){QTextStream(stdout)<<VERSION<<'\n';return 0;}
    QString folder=QStandardPaths::writableLocation(QStandardPaths::AppConfigLocation);
    if(!QDir().mkpath(folder)){QMessageBox::critical(nullptr,ui("无法启动"),ui("无法创建用户配置目录，请检查目录权限。"));return 1;}
    QFile::setPermissions(folder,QFile::ReadOwner|QFile::WriteOwner|QFile::ExeOwner);
    QByteArray lockPath=QFile::encodeName(folder+"/instance.lock");int fd=open(lockPath.constData(),O_CREAT|O_RDWR|O_NOFOLLOW,0600);
    if(fd<0||flock(fd,LOCK_EX|LOCK_NB)!=0){if(fd>=0)close(fd);QMessageBox::information(nullptr,ui("程序已在运行"),ui("请右键计时小窗，或双击托盘中的蓝色计时图标打开设置。"));return 0;}
    Controller controller(folder);
    if(args.contains("--demo"))controller.demo();
    int screenshotIndex=args.indexOf("--screenshot");if(screenshotIndex>=0&&screenshotIndex+1<args.size())QTimer::singleShot(1500,&app,[&]{controller.screenshot(args[screenshotIndex+1]);app.quit();});
    int settingsIndex=args.indexOf("--screenshot-settings");if(settingsIndex>=0&&settingsIndex+1<args.size()){QTimer::singleShot(300,&app,[&]{controller.openSettings();});QTimer::singleShot(2000,&app,[&]{int tab=0,ti=args.indexOf("--settings-tab");if(ti>=0&&ti+1<args.size())tab=args[ti+1].toInt();controller.settingsScreenshot(args[settingsIndex+1],tab);app.quit();});}
    int code=app.exec();flock(fd,LOCK_UN);close(fd);return code;
}
