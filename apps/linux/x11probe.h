#pragma once
#include "appdata.h"
#include <functional>
namespace wpsclock {
struct WindowInfo {
    unsigned long id=0,owner=0;int pid=0;
    QString title,klass,file,reason;
    QRect bounds;
    bool foreground=false,show=false;
    Match match;
};
class X11Probe {
    struct Impl;Impl *p;
public:
    QString warning;
    explicit X11Probe();~X11Probe();
    bool available() const;
    QVector<WindowInfo> scan(const Config &config,const QList<QRect> &screens);
    bool hotkeys(int pause,int reset);
    void drain(const std::function<void(int)> &action);
    void top(unsigned long window,bool locked);
    bool composite() const;
};
int selectShow(const QVector<WindowInfo> &windows,unsigned long previous);
}
