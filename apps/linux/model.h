#pragma once
#include <algorithm>
#include <cmath>
#include <cstdio>
#include <string>

namespace wpsclock {
struct Clock {
    bool active=false, paused=false, warned=false, ended=false;
    int duration=480;
    double anchor=0, accumulated=0;
    void start(double now,int seconds) { active=true;paused=warned=ended=false;duration=std::max(0,seconds);anchor=now;accumulated=0; }
    double elapsed(double now) const {return active?accumulated+(paused?0:std::max(0.0,now-anchor)):0;}
    void pause(double now) {if(active&&!paused){accumulated=elapsed(now);paused=true;}}
    void resume(double now) {if(active&&paused){anchor=now;paused=false;}}
    void toggle(double now) {if(paused)resume(now);else pause(now);}
    void stop() {*this=Clock();}
    void changeDuration(double now,int seconds) {
        if(duration==seconds)return;
        bool frozen=ended&&paused;duration=std::max(0,seconds);warned=ended=false;
        if(frozen)resume(now);
    }
    int advance(double now,int warning,bool stopAtZero) {
        if(!active||paused||duration==0)return 0;
        const double e=elapsed(now);
        if(e>=duration&&!ended){ended=true;if(stopAtZero){accumulated=duration;paused=true;}return 2;}
        if(!warned&&!ended&&warning>0&&e>=std::max(0,duration-warning)){warned=true;return 1;}
        return 0;
    }
    static std::string format(long long seconds) {
        seconds=std::max(0LL,seconds);char b[64];std::snprintf(b,sizeof b,"%02lld:%02lld",seconds/60,seconds%60);return b;
    }
    std::string display(double now,bool countUp,const std::string &prefix="-") const {
        double e=elapsed(now);
        if(countUp||duration==0)return format(static_cast<long long>(std::floor(e)));
        if(e<=duration)return format(static_cast<long long>(std::ceil(duration-e)));
        return prefix+format(static_cast<long long>(std::floor(e-duration)));
    }
};
inline std::string basename(const std::string &s) {auto p=s.find_last_of('/');return p==std::string::npos?s:s.substr(p+1);}
inline bool sameFile(const std::string &a,const std::string &b) {
    if(a.empty()||b.empty())return false;
    if(a.find('/')!=std::string::npos&&b.find('/')!=std::string::npos)return a==b;
    return basename(a)==basename(b);
}
struct Observation {
    bool present=false, background=false, ambiguous=false, matched=false;
    std::string window,file;
    int seconds=480;
};
struct Session {
    Clock clock;
    bool monitoring=true,manual=false,held=false,wasRunning=false,manualDuration=false;
    std::string file,window,suppressed,candidate;
    double candidateAt=-1,lastCandidate=-1,missingAt=-1;
    int grace=3;
    static std::string key(const Observation &o) {return o.file.empty()?"w:"+o.window:"f:"+o.file;}
    bool stable(const std::string &next,double now) {
        if(candidate!=next||now-lastCandidate>1.2){candidate=next;candidateAt=lastCandidate=now;return false;}
        lastCandidate=now;return now-candidateAt>=0.35;
    }
    void clearCandidate(){candidate.clear();candidateAt=lastCandidate=-1;}
    void begin(double now,const Observation &o) {
        clock.start(now,o.seconds);file=o.file;window=o.window;
        manual=held=manualDuration=false;missingAt=-1;clearCandidate();
    }
    void startManual(double now,int seconds) {
        Observation o;o.seconds=seconds;begin(now,o);manual=true;suppressed.clear();
    }
    void stop() {
        suppressed=file.empty()?"w:"+window:"f:"+file;
        clock.stop();held=manual=manualDuration=false;file.clear();window.clear();missingAt=-1;clearCandidate();
    }
    void reset(double now,int fallback) {
        if(held){held=false;manual=true;}
        if(!clock.active)startManual(now,fallback);else clock.start(now,manualDuration?clock.duration:fallback);
    }
    void toggle(double now,int fallback) {
        if(held){held=false;manual=true;}
        if(!clock.active)startManual(now,fallback);else clock.toggle(now);
    }
    void choose(double now,int seconds) {clock.changeDuration(now,seconds);manualDuration=true;}
    void observe(double now,const Observation &o) {
        if(!monitoring||manual)return;
        if(!o.present){
            clearCandidate();suppressed.clear();
            if(held||!clock.active)return;
            if(missingAt<0)missingAt=now;
            if(now-missingAt>=std::max(0.8,double(grace))){stop();suppressed.clear();}
            return;
        }
        missingAt=-1;
        if(!suppressed.empty()) {if(suppressed==key(o))return;suppressed.clear();}
        if(o.background&&o.matched&&!o.ambiguous){
            if(held){clearCandidate();return;}
            if(!stable("background:"+key(o),now))return;
            wasRunning=clock.active&&!clock.paused;clock.pause(now);held=true;clearCandidate();return;
        }
        if(held){
            if(o.file.empty()||o.ambiguous){clearCandidate();return;}
            if(!stable("return:"+key(o),now))return;
            if(clock.active&&sameFile(file,o.file)) {
                held=false;window=o.window;if(wasRunning)clock.resume(now);clearCandidate();
            }else begin(now,o);
            return;
        }
        if(!clock.active){if(stable("start:"+key(o),now))begin(now,o);return;}
        if(o.file.empty()||o.ambiguous){clearCandidate();return;}
        if(file.empty()||sameFile(file,o.file)){
            clearCandidate();window=o.window;
            if(file.empty()||(file.find('/')==std::string::npos&&o.file.find('/')!=std::string::npos)){
                file=o.file;if(!manualDuration)clock.changeDuration(now,o.seconds);
            }
            return;
        }
        if(stable("switch:"+key(o),now))begin(now,o);
    }
};
}
