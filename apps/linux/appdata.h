#pragma once
#include <QtCore>
#include <QtGui>
#include "model.h"
#include "i18n.h"

namespace wpsclock {
inline std::string bytes(const QString &s){auto b=s.toUtf8();return std::string(b.constData(),size_t(b.size()));}
inline QString text(const std::string &s){return QString::fromUtf8(s.data(),int(s.size()));}
struct Rule {QString file;int seconds=480;bool background=false;};
struct Config {
    int duration=480,warning=60,grace=3,width=260,height=110,fontSize=42,opacity=90;
    int pauseKey=3,resetKey=4;
    bool countUp=false,stopAtZero=false,automatic=true,syncScreens=true,locked=false,hideIdle=false;
    bool nearSound=true,endSound=true,clockTime=false;
    QString nearFile,endFile,prefix="-",language="system";
    QColor foreground=Qt::white,background=QColor("#0078d7"),warningForeground=Qt::white,warningBackground=QColor("#b42318");
    QVector<Rule> rules;
    QJsonObject positions;
    static Config load(const QString &path,QString &error) {
        Config c;QFile f(path);if(!f.exists())return c;
        if(!f.open(QIODevice::ReadOnly)){error=ui("无法读取设置，当前使用默认值：")+f.errorString();return c;}
        if(f.size()>2*1024*1024){error=ui("设置文件过大，未读取；原文件已保留。");return c;}
        QJsonParseError pe;auto doc=QJsonDocument::fromJson(f.readAll(),&pe);
        if(pe.error!=QJsonParseError::NoError||!doc.isObject()){error=ui("设置文件损坏，当前使用默认值；原文件已保留。");return c;}
        auto o=doc.object();if(o.value("schema").toInt()!=1){error=ui("设置格式版本不支持，原文件已保留。");return c;}
        auto num=[&](const char *k,int d,int lo,int hi){return qBound(lo,o.value(k).toInt(d),hi);};
        c.language=UiText::normalize(o.value("language").toString("system"));
        c.duration=num("duration",480,0,86399);c.warning=num("warning",60,0,86399);c.grace=num("grace",3,0,120);
        c.width=num("width",260,140,1600);c.height=num("height",110,70,900);c.fontSize=num("fontSize",42,16,120);c.opacity=num("opacity",90,0,100);
        c.pauseKey=num("pauseKey",3,1,12);c.resetKey=num("resetKey",4,1,12);
        if(c.pauseKey==c.resetKey){c.pauseKey=3;c.resetKey=4;}
        c.countUp=o.value("countUp").toBool();c.stopAtZero=o.value("stopAtZero").toBool();c.automatic=o.value("automatic").toBool(true);
        c.syncScreens=o.value("syncScreens").toBool(true);c.locked=o.value("locked").toBool();c.hideIdle=o.value("hideIdle").toBool();
        c.nearSound=o.value("nearSound").toBool(true);c.endSound=o.value("endSound").toBool(true);c.clockTime=o.value("clockTime").toBool();
        c.nearFile=o.value("nearFile").toString();c.endFile=o.value("endFile").toString();c.prefix=o.value("prefix").toString("-").left(20);
        auto color=[&](const char *k,QColor d){QColor a(o.value(k).toString());return a.isValid()?a:d;};
        c.foreground=color("foreground",c.foreground);c.background=color("background",c.background);
        c.warningForeground=color("warningForeground",c.warningForeground);c.warningBackground=color("warningBackground",c.warningBackground);
        for(auto v:o.value("rules").toArray()){
            auto r=v.toObject();Rule rule;rule.file=r.value("file").toString().trimmed();rule.seconds=qBound(0,r.value("seconds").toInt(480),86399);rule.background=r.value("background").toBool();
            if(!rule.file.isEmpty()&&c.rules.size()<1000)c.rules.append(rule);
        }
        c.positions=o.value("positions").toObject();return c;
    }
    bool save(const QString &path,QString &error) const {
        QJsonObject o; o["schema"]=1;o["language"]=UiText::normalize(language);
        o["duration"]=duration;o["warning"]=warning;o["grace"]=grace;o["width"]=width;o["height"]=height;o["fontSize"]=fontSize;o["opacity"]=opacity;
        o["pauseKey"]=pauseKey;o["resetKey"]=resetKey;o["countUp"]=countUp;o["stopAtZero"]=stopAtZero;o["automatic"]=automatic;o["syncScreens"]=syncScreens;
        o["locked"]=locked;o["hideIdle"]=hideIdle;o["nearSound"]=nearSound;o["endSound"]=endSound;o["clockTime"]=clockTime;
        o["nearFile"]=nearFile;o["endFile"]=endFile;o["prefix"]=prefix;
        o["foreground"]=foreground.name();o["background"]=background.name();o["warningForeground"]=warningForeground.name();o["warningBackground"]=warningBackground.name();
        QJsonArray a;for(const auto &r:rules){QJsonObject v;v["file"]=r.file;v["seconds"]=r.seconds;v["background"]=r.background;a.append(v);}o["rules"]=a;o["positions"]=positions;
        QSaveFile f(path);if(!f.open(QIODevice::WriteOnly)){error=f.errorString();return false;}
        f.setPermissions(QFile::ReadOwner|QFile::WriteOwner);auto data=QJsonDocument(o).toJson();
        if(f.write(data)!=data.size()||!f.commit()){error=f.errorString();return false;}return true;
    }
};
struct Match {
    QString file,explanation;
    int seconds=480,index=-1;
    bool matched=false,ambiguous=false,background=false;
};
inline QString cleanTitle(QString title) {
    title=title.trimmed();
    title.remove(QRegularExpression("^(?:WPS\\s+Presentation\\s+Slide\\s+Show|幻灯片放映|WPS\\s*演示\\s*(?:幻灯片\\s*)?放映)[\\s:：\\-—–]*",QRegularExpression::CaseInsensitiveOption));
    title.remove(QRegularExpression("\\s*[-—–|]\\s*(?:WPS(?:\\s*(?:Office|演示|Presentation|PDF))?)\\s*$",QRegularExpression::CaseInsensitiveOption));
    title.remove(QRegularExpression("\\s*[\\[【(（](?:只读|兼容模式|Read.Only|Compatibility Mode)[\\]】)）]\\s*",QRegularExpression::CaseInsensitiveOption));
    title=title.trimmed();
    const QString left="[［【",right="]］】";
    while(title.size()>1&&left.contains(title.front())&&right.contains(title.back()))title=title.mid(1,title.size()-2).trimmed();
    return title;
}
inline QString normalized(QString file){return QDir::cleanPath(file.trimmed());}
inline Match matchTitle(const Config &c,const QString &title,const QString &known=QString()) {
    Match m;m.seconds=c.duration;const QString caption=cleanTitle(title);
    if(!known.isEmpty())m.file=normalized(known);
    else if(QRegularExpression("\\.(pptx?|ppsx?|pdf)$",QRegularExpression::CaseInsensitiveOption).match(caption).hasMatch())m.file=normalized(caption);
    QVector<int> hits;
    if(!m.file.isEmpty()&&QDir::isAbsolutePath(m.file)){
        for(int i=0;i<c.rules.size();++i)if(normalized(c.rules[i].file)==m.file)hits.append(i);
        if(hits.isEmpty()){m.explanation="已获得完整路径，但没有对应规则";return m;}
    }else{
        for(int i=0;i<c.rules.size();++i){
            const auto name=QFileInfo(c.rules[i].file).fileName();
            if((!m.file.isEmpty()&&QFileInfo(m.file).fileName()==name)||
               (m.file.isEmpty()&&!caption.isEmpty()&&(caption==name||caption==QFileInfo(name).completeBaseName())))hits.append(i);
        }
    }
    if(hits.size()==1){const auto &r=c.rules[hits[0]];m.index=hits[0];m.matched=true;m.seconds=r.seconds;m.background=r.background;
        if(m.file.isEmpty())m.file=QFileInfo(r.file).fileName();m.explanation=QDir::isAbsolutePath(m.file)?"完整路径匹配":"唯一文件名匹配";
    }else if(hits.size()>1){m.ambiguous=true;m.explanation="同名文件有歧义，使用默认时长";}
    else m.explanation=m.file.isEmpty()?"未识别文件，使用默认时长":"未设置文件规则，使用默认时长";
    return m;
}
inline QByteArray wave(bool final) {
    QByteArray pcm;const int rate=22050;QVector<int> notes=final?QVector<int>{880,880,1175}:QVector<int>{880,1100};
    QDataStream stream(&pcm,QIODevice::WriteOnly);stream.setByteOrder(QDataStream::LittleEndian);
    for(int n=0;n<notes.size();++n){int count=int(rate*(n==notes.size()-1?.4:.2));
        for(int i=0;i<count;++i){double env=qMax(0.0,qMin(1.0,qMin(i/(rate*.015),(count-i)/(rate*.04))));stream<<qint16(std::sin(2*3.141592653589793*notes[n]*i/rate)*10000*env);}
        for(int i=0;i<rate/9;++i)stream<<qint16(0);
    }
    QByteArray result;QDataStream out(&result,QIODevice::WriteOnly);out.setByteOrder(QDataStream::LittleEndian);
    out.writeRawData("RIFF",4);out<<quint32(36+pcm.size());out.writeRawData("WAVEfmt ",8);out<<quint32(16)<<quint16(1)<<quint16(1)<<quint32(rate)<<quint32(rate*2)<<quint16(2)<<quint16(16);
    out.writeRawData("data",4);out<<quint32(pcm.size());out.writeRawData(pcm.constData(),pcm.size());return result;
}
}
