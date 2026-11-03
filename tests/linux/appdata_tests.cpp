#include "../../apps/linux/appdata.h"
#include <iostream>
#include <cstdlib>
using namespace wpsclock;
static int count=0;
static void check(bool ok,const char *name){if(!ok){std::cerr<<"FAIL "<<name<<'\n';std::exit(1);}++count;}
int main(int argc,char **argv){
    QCoreApplication app(argc,argv);
    check(UiText::resolve("system","zh_CN")=="zh-CN"&&UiText::resolve("system","en_GB")=="en","system language detection");
    check(UiText::resolve("system","fr_FR")=="en"&&UiText::normalize("invalid")=="system","language fallback");
    UiText::useLanguage("en");check(ui("保存")=="Save"&&ui("会议背景")=="Background","English catalog");
    check(ui(QString("/会议/会议背景.pptx"))=="/会议/会议背景.pptx","paths remain unchanged");
    check(ui("第 %1 行文件名为空、包含换行或重复。").arg(2)=="Row 2 has an empty filename, a newline or a duplicate.","translated placeholders");
    UiText::useLanguage("zh-CN");check(ui("保存")=="保存","switch back to Chinese");
    Config c;Rule r;r.file="/meetings/发言A.pptx";r.seconds=120;c.rules.append(r);
    auto m=matchTitle(c,"WPS Presentation Slide Show -［发言A.pptx］");check(m.matched&&m.seconds==120&&m.file=="发言A.pptx","Chinese WPS title");
    m=matchTitle(c,"WPS演示 幻灯片放映 - [发言A.pptx]");check(m.matched,"Chinese prefix");
    m=matchTitle(c,"发言A - WPS 演示");check(m.matched,"hidden extension");
    m=matchTitle(c,"发言A.pptx [只读] - WPS Office");check(m.matched,"read only annotation");
    r.file="/other/发言A.pptx";r.seconds=300;c.rules.append(r);
    m=matchTitle(c,"发言A.pptx - WPS 演示");check(m.ambiguous&&!m.matched&&m.seconds==480,"duplicate filename fallback");
    m=matchTitle(c,QString(),"/other/发言A.pptx");check(m.matched&&m.seconds==300,"exact full path");
    m=matchTitle(c,QString(),"/unknown/发言A.pptx");check(!m.matched,"known different full path never matches");
    r.file="Theme.pptx";r.background=true;c.rules.append(r);
    m=matchTitle(c,"Theme.pptx - WPS 演示");check(m.background&&m.matched,"background rule");
    m=matchTitle(c,"theme.pptx - WPS 演示");check(!m.matched,"Linux path case sensitivity");
    m=matchTitle(c,"WPS Presentation Slide Show");check(!m.matched&&m.file.isEmpty(),"no identity never guesses");
    check(cleanTitle("WPS演示放映 - 【报告.pptx】")=="报告.pptx","bracket normalization");
    check(normalized("/a/./b/../c.pdf")=="/a/c.pdf","Linux path normalization");
    QTemporaryDir dir;check(dir.isValid(),"temporary directory");QString path=dir.path()+"/settings.json",error;
    c.language="en";c.duration=3600;c.opacity=40;c.prefix="超时 ";check(c.save(path,error),"atomic save");
    auto loaded=Config::load(path,error);check(error.isEmpty()&&loaded.language=="en"&&loaded.duration==3600&&loaded.rules.size()==3&&loaded.rules[2].background&&loaded.prefix=="超时 "&&loaded.opacity==40,"settings roundtrip");
    auto permissions=QFile::permissions(path);check(!(permissions&QFile::ReadOther),"private settings permissions");
    QFile f(path);check(f.open(QIODevice::WriteOnly|QIODevice::Truncate),"open corruption fixture");f.write("not JSON");f.close();error.clear();
    loaded=Config::load(path,error);check(!error.isEmpty()&&loaded.duration==480,"corrupt settings fallback");check(f.open(QIODevice::ReadOnly)&&f.readAll()=="not JSON","corrupt settings retained");f.close();
    QJsonObject malformed;malformed["schema"]=1;malformed["duration"]=-5;malformed["width"]=100000;malformed["opacity"]=999;malformed["pauseKey"]=3;malformed["resetKey"]=3;
    f.open(QIODevice::WriteOnly|QIODevice::Truncate);f.write(QJsonDocument(malformed).toJson());f.close();error.clear();loaded=Config::load(path,error);
    check(loaded.duration==0&&loaded.width==1600&&loaded.opacity==100,"invalid numeric settings clamped");check(loaded.pauseKey!=loaded.resetKey,"duplicate loaded shortcuts repaired");
    for(bool final:{false,true}){auto data=wave(final);QDataStream stream(data);stream.setByteOrder(QDataStream::LittleEndian);char tag[4];stream.readRawData(tag,4);quint32 size;stream>>size;check(QByteArray(tag,4)=="RIFF"&&size==quint32(data.size()-8),"WAV RIFF length");check(data.mid(8,8)=="WAVEfmt "&&data.mid(36,4)=="data","WAV format");check(data.size()>22050,"nonempty audio");}
    std::cout<<"PASS "<<count<<" application data checks\n";
}
