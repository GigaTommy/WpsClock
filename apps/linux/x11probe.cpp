#include "x11probe.h"
#include <X11/Xlib.h>
#include <X11/Xatom.h>
#include <X11/keysym.h>
#include <X11/XKBlib.h>
#include <X11/Xutil.h>
#include <X11/extensions/shape.h>
#include <unistd.h>
#include <cstring>
namespace wpsclock {
struct X11Probe::Impl {
    Display *display=nullptr;Window root=0;int pauseCode=0,resetCode=0;unsigned int numMask=0;
    QMap<QString,Atom> atoms;QMap<unsigned long,QString> bound;QSet<unsigned int> pressed;
    Atom atom(const char *s){QString key=QString::fromLatin1(s);if(!atoms.contains(key))atoms[key]=XInternAtom(display,s,False);return atoms[key];}
    QVector<unsigned long> numbers(Window w,const char *name,Atom type=XA_CARDINAL){
        QVector<unsigned long> out;Atom actual=0;int format=0;unsigned long count=0,left=0;unsigned char *data=nullptr;
        if(XGetWindowProperty(display,w,atom(name),0,4096,False,type,&actual,&format,&count,&left,&data)==0&&actual==type&&format==32){
            auto values=reinterpret_cast<unsigned long*>(data);for(unsigned long i=0;i<count;++i)out.append(values[i]);}
        if(data)XFree(data);return out;
    }
    QString title(Window w){
        Atom actual=0;int format=0;unsigned long count=0,left=0;unsigned char *data=nullptr;QString result;
        if(XGetWindowProperty(display,w,atom("_NET_WM_NAME"),0,4096,False,atom("UTF8_STRING"),&actual,&format,&count,&left,&data)==0&&format==8)result=QString::fromUtf8(reinterpret_cast<char*>(data),int(count));
        if(data)XFree(data);
        if(result.isEmpty()){char *name=nullptr;if(XFetchName(display,w,&name)&&name){result=QString::fromLocal8Bit(name);XFree(name);}}
        return result;
    }
};
static int lastXError=0;
static int xError(Display *,XErrorEvent *e){lastXError=e->error_code;return 0;}
X11Probe::X11Probe():p(new Impl){
    // One dedicated connection, used only on the Qt main thread. Expired windows are normal.
    XSetErrorHandler(xError);p->display=XOpenDisplay(nullptr);
    if(p->display){p->root=DefaultRootWindow(p->display);Bool supported=False;XkbSetDetectableAutoRepeat(p->display,True,&supported);}
    else warning="X11 连接不可用：自动检测和全局快捷键不可用，请手动控制。";
}
X11Probe::~X11Probe(){if(p->display){XUngrabKey(p->display,AnyKey,AnyModifier,p->root);XCloseDisplay(p->display);}delete p;}
bool X11Probe::available() const {return p->display!=nullptr;}
bool X11Probe::composite() const {return available()&&XGetSelectionOwner(p->display,p->atom("_NET_WM_CM_S0"))!=0;}
QVector<WindowInfo> X11Probe::scan(const Config &config,const QList<QRect> &screens){
    QVector<WindowInfo> out;if(!available())return out;
    auto clients=p->numbers(p->root,"_NET_CLIENT_LIST_STACKING",XA_WINDOW);
    if(clients.isEmpty())clients=p->numbers(p->root,"_NET_CLIENT_LIST",XA_WINDOW);
    auto active=p->numbers(p->root,"_NET_ACTIVE_WINDOW",XA_WINDOW);Window front=active.isEmpty()?0:active.first();
    QSet<unsigned long> alive;
    for(auto id:clients){
        XWindowAttributes attrs;if(!XGetWindowAttributes(p->display,id,&attrs)||attrs.map_state!=IsViewable)continue;
        WindowInfo w;w.id=id;alive.insert(id);auto pid=p->numbers(id,"_NET_WM_PID");w.pid=pid.isEmpty()?0:int(pid.first());
        if(w.pid==getpid())continue;
        XClassHint hint={};if(XGetClassHint(p->display,id,&hint)){
            w.klass=QString::fromLocal8Bit(hint.res_name?hint.res_name:"")+" / "+QString::fromLocal8Bit(hint.res_class?hint.res_class:"");
            if(hint.res_name)XFree(hint.res_name);if(hint.res_class)XFree(hint.res_class);
        }
        // Class/process identity is required: an unrelated fullscreen window must not start a speech.
        const QString klass=w.klass.toLower();QString process;
        if(w.pid>0){QFile f(QString("/proc/%1/comm").arg(w.pid));if(f.open(QIODevice::ReadOnly))process=QString::fromLocal8Bit(f.read(128)).trimmed().toLower();}
        bool office=klass.contains("wpp")||klass.contains("wps")||klass.contains("kingsoft")||process=="wpp"||process=="wps"||process=="wpspdf";
        if(!office)continue;
        w.title=p->title(id);w.foreground=id==front;Window owner=0;if(XGetTransientForHint(p->display,id,&owner))w.owner=owner;
        int x=0,y=0;Window child=0;XTranslateCoordinates(p->display,id,p->root,0,0,&x,&y,&child);w.bounds=QRect(x,y,attrs.width,attrs.height);
        auto state=p->numbers(id,"_NET_WM_STATE",XA_ATOM);bool full=state.contains(p->atom("_NET_WM_STATE_FULLSCREEN"));
        bool hidden=state.contains(p->atom("_NET_WM_STATE_HIDDEN"));bool covers=false;
        for(const QRect &r:screens)if(qAbs(r.x()-x)<=8&&qAbs(r.y()-y)<=8&&qAbs(r.width()-attrs.width)<=16&&qAbs(r.height()-attrs.height)<=16)covers=true;
        bool presenter=w.title.contains("演讲者")||w.title.contains("presenter",Qt::CaseInsensitive);
        bool explicitShow=klass.contains("slideshow")||klass.contains("wppshow")||w.title.contains("幻灯片放映")||w.title.contains("演示放映")||w.title.startsWith("WPS Presentation Slide Show",Qt::CaseInsensitive);
        // Fullscreen state alone is insufficient for ordinary WPS Writer/editor windows.
        bool presentation=klass.contains("wpp")||process=="wpp"||klass.contains("wpspdf")||process=="wpspdf";
        auto titleMatch=matchTitle(config,w.title);bool pdf=titleMatch.file.endsWith(".pdf",Qt::CaseInsensitive);
        w.show=!hidden&&!presenter&&covers&&(explicitShow||(full&&(presentation||pdf)));
        w.reason=w.show?(explicitShow?"WPS 放映特征 + 全屏几何":"WPS 演示/PDF + 全屏状态"):"编辑窗口或未确认放映";
        w.match=titleMatch;w.file=titleMatch.file;out.append(w);
    }
    for(auto it=p->bound.begin();it!=p->bound.end();)if(!alive.contains(it.key()))it=p->bound.erase(it);else ++it;
    for(auto &w:out){if(!w.show)continue;
        if(w.file.isEmpty()){
            QSet<QString> names;QString ownerFile;int showCount=0;
            for(const auto &other:out)if(w.pid>0&&other.pid==w.pid){
                if(other.show)++showCount;
                else if(!other.file.isEmpty()&&!other.match.ambiguous){names.insert(other.file);if(w.owner==other.id)ownerFile=other.file;}
            }
            if(!ownerFile.isEmpty())w.file=ownerFile;
            else if(showCount==1&&names.size()==1)w.file=*names.constBegin();
            else if(p->bound.contains(w.id))w.file=p->bound[w.id];
            if(!w.file.isEmpty())w.match=matchTitle(config,w.title,w.file);
        }
        if(!w.file.isEmpty()&&!w.match.ambiguous)p->bound[w.id]=w.file;
    }
    return out;
}
bool X11Probe::hotkeys(int pause,int reset){
    if(!available())return false;
    XUngrabKey(p->display,AnyKey,AnyModifier,p->root);p->pauseCode=p->resetCode=0;p->pressed.clear();XSync(p->display,False);
    auto mod=XGetModifierMapping(p->display);p->numMask=0;auto num=XKeysymToKeycode(p->display,XK_Num_Lock);
    if(mod){for(int i=0;i<8;++i)for(int j=0;j<mod->max_keypermod;++j)if(mod->modifiermap[i*mod->max_keypermod+j]==num)p->numMask|=1U<<i;XFreeModifiermap(mod);}
    int pc=XKeysymToKeycode(p->display,XK_F1+pause-1),rc=XKeysymToKeycode(p->display,XK_F1+reset-1);
    if(!pc||!rc||pc==rc){warning="快捷键设置无效，请在设置中更换。";return false;}
    lastXError=0;QSet<unsigned int> masks={0,LockMask,p->numMask,LockMask|p->numMask};
    for(auto mask:masks){XGrabKey(p->display,pc,mask,p->root,False,GrabModeAsync,GrabModeAsync);XGrabKey(p->display,rc,mask,p->root,False,GrabModeAsync,GrabModeAsync);}
    XSync(p->display,False);
    if(lastXError){XUngrabKey(p->display,AnyKey,AnyModifier,p->root);warning="全局快捷键被占用，暂停/重置请用右键菜单，或在设置中更换按键。";return false;}
    p->pauseCode=pc;p->resetCode=rc;warning.clear();return true;
}
void X11Probe::drain(const std::function<void(int)> &action){
    if(!available())return;
    int limit=256;while(limit--&&XPending(p->display)){XEvent e;XNextEvent(p->display,&e);
        if(e.type==KeyRelease){
            if(XPending(p->display)){XEvent next;XPeekEvent(p->display,&next);if(next.type==KeyPress&&next.xkey.keycode==e.xkey.keycode&&next.xkey.time==e.xkey.time){XNextEvent(p->display,&next);continue;}}
            p->pressed.remove(e.xkey.keycode);continue;
        }
        if(e.type!=KeyPress||p->pressed.contains(e.xkey.keycode))continue;
        p->pressed.insert(e.xkey.keycode);
        if(int(e.xkey.keycode)==p->pauseCode)action(1);else if(int(e.xkey.keycode)==p->resetCode)action(2);
    }
}
void X11Probe::top(unsigned long id,bool locked){
    if(!available())return;
    XEvent e={};e.xclient.type=ClientMessage;e.xclient.window=id;e.xclient.message_type=p->atom("_NET_WM_STATE");e.xclient.format=32;
    e.xclient.data.l[0]=1;e.xclient.data.l[1]=long(p->atom("_NET_WM_STATE_ABOVE"));e.xclient.data.l[2]=long(p->atom("_NET_WM_STATE_SKIP_TASKBAR"));e.xclient.data.l[3]=1;
    XSendEvent(p->display,p->root,False,SubstructureRedirectMask|SubstructureNotifyMask,&e);
    if(locked)XShapeCombineRectangles(p->display,id,ShapeInput,0,0,nullptr,0,ShapeSet,Unsorted);
    else XShapeCombineMask(p->display,id,ShapeInput,0,0,0,ShapeSet);
    XRaiseWindow(p->display,id);XFlush(p->display);
}
int selectShow(const QVector<WindowInfo> &windows,unsigned long previous){
    int retained=-1,first=-1,count=0;
    for(int i=0;i<windows.size();++i)if(windows[i].show){++count;if(windows[i].foreground)return i;if(first<0)first=i;if(windows[i].id==previous)retained=i;}
    for(const auto &front:windows)if(front.foreground&&!front.show&&(front.title.contains("演讲者")||front.title.contains("presenter",Qt::CaseInsensitive))){
        int chosen=-1,matches=0;for(int i=0;i<windows.size();++i){const auto &w=windows[i];if(!w.show||w.pid<=0||w.pid!=front.pid)continue;
            if(!front.file.isEmpty()&&!w.file.isEmpty()&&front.file!=w.file)continue;
            if((!front.file.isEmpty()&&front.file==w.file)||w.owner==front.id||(front.owner&&front.owner==w.owner)){chosen=i;++matches;}}
        if(matches==1)return chosen;
    }
    return retained>=0?retained:(count==1?first:-1);
}
}
