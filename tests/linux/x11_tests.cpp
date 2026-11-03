#include "../../apps/linux/x11probe.h"
#include <X11/Xlib.h>
#include <X11/Xatom.h>
#include <X11/Xutil.h>
#include <X11/keysym.h>
#include <X11/extensions/shape.h>
#include <unistd.h>
#include <iostream>
#include <cstdlib>
using namespace wpsclock;
static int count=0;
static void check(bool ok,const char *label){if(!ok){std::cerr<<"FAIL "<<label<<'\n';std::exit(1);}++count;}
static void settle(){QThread::msleep(250);}
int main(int argc,char **argv){
    QCoreApplication app(argc,argv);Display *d=XOpenDisplay(nullptr);check(d!=nullptr,"X display");auto root=DefaultRootWindow(d);
    auto atom=[&](const char *s){return XInternAtom(d,s,False);};
    auto create=[&](const char *name,const char *klass,bool full){
        Window w=XCreateSimpleWindow(d,root,0,0,full?1280:700,full?800:500,0,0,0xffffff);
        XClassHint hint={const_cast<char*>(klass),const_cast<char*>(klass)};XSetClassHint(d,w,&hint);
        XChangeProperty(d,w,atom("_NET_WM_NAME"),atom("UTF8_STRING"),8,PropModeReplace,reinterpret_cast<const unsigned char*>(name),int(strlen(name)));
        unsigned long pid=getpid()+10000;XChangeProperty(d,w,atom("_NET_WM_PID"),XA_CARDINAL,32,PropModeReplace,reinterpret_cast<unsigned char*>(&pid),1);
        if(full){Atom state=atom("_NET_WM_STATE_FULLSCREEN");XChangeProperty(d,w,atom("_NET_WM_STATE"),XA_ATOM,32,PropModeReplace,reinterpret_cast<unsigned char*>(&state),1);}
        XMapWindow(d,w);XSync(d,False);settle();return w;
    };
    X11Probe probe;check(probe.available(),"probe connected");Config c;Rule a;a.file="A.pptx";a.seconds=120;c.rules.append(a);
    auto editor=create("A.pptx - WPS 演示","wpp",false);
    auto windows=probe.scan(c,{QRect(0,0,1280,800)});check(windows.size()==1&&!windows[0].show,"ordinary editor does not trigger");
    auto browser=create("A.pptx - Browser","browser",true);windows=probe.scan(c,{QRect(0,0,1280,800)});check(windows.size()==1,"other fullscreen application excluded");
    XDestroyWindow(d,browser);XSync(d,False);settle();
    auto show=create("WPS Presentation Slide Show - [A.pptx]","wpp",true);windows=probe.scan(c,{QRect(0,0,1280,800)});
    int index=selectShow(windows,0);check(index>=0&&windows[index].show&&windows[index].file=="A.pptx"&&windows[index].match.seconds==120,"fullscreen identified with rule");
    auto retained=windows[index].id;check(selectShow(windows,retained)>=0,"retain existing show");
    probe.top(show,true);XSync(d,False);settle();int rectangles=-1,ordering=0;auto *rects=XShapeGetRectangles(d,show,ShapeInput,&rectangles,&ordering);if(rects)XFree(rects);check(rectangles==0,"click-through input region empty");
    probe.top(show,false);XSync(d,False);rects=XShapeGetRectangles(d,show,ShapeInput,&rectangles,&ordering);if(rects)XFree(rects);check(rectangles>0,"click-through can be unlocked");
    check(probe.hotkeys(3,4),"global shortcut registration");int pauses=0,resets=0;auto drain=[&]{settle();probe.drain([&](int action){if(action==1)++pauses;else ++resets;});};
    QProcess::execute("xdotool",{"keydown","F3"});drain();check(pauses==1,"F3 delivered globally");
    QProcess::execute("xdotool",{"keydown","F3"});drain();check(pauses==1,"held key does not toggle repeatedly");
    QProcess::execute("xdotool",{"keyup","F3"});drain();QProcess::execute("xdotool",{"key","F3"});drain();check(pauses==2,"new keypress works after release");
    QProcess::execute("xdotool",{"key","F4"});drain();check(resets==1,"F4 delivered globally");
    check(!probe.hotkeys(3,3),"duplicate shortcut rejected");
    XGrabKey(d,XKeysymToKeycode(d,XK_F3),0,root,False,GrabModeAsync,GrabModeAsync);XSync(d,False);check(!probe.hotkeys(3,4),"occupied shortcut reports failure");
    XUngrabKey(d,AnyKey,AnyModifier,root);XSync(d,False);check(probe.hotkeys(3,4),"shortcut retry recovers");
    XDestroyWindow(d,show);XDestroyWindow(d,editor);XSync(d,False);settle();check(probe.scan(c,{QRect(0,0,1280,800)}).isEmpty(),"destroyed windows do not crash probe");
    XCloseDisplay(d);std::cout<<"PASS "<<count<<" X11 integration checks\n";
}
