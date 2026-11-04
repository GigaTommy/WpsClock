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
    enum class Change {Unchanged,Started,Stopped,Background};
    Clock clock;
    bool monitoring=true,manual=false,held=false,wasRunning=false,manualDuration=false;
    std::string file,window,suppressedFile,suppressedWindow,candidate;
    double candidateAt=-1,lastCandidate=-1,missingAt=-1;
    int grace=3;
    static std::string key(const Observation &o) {return o.file.empty()?"w:"+o.window:"f:"+o.file;}
    bool stable(const std::string &next,double now) {
        if(candidate!=next||now-lastCandidate>1.2){candidate=next;candidateAt=lastCandidate=now;return false;}
        lastCandidate=now;return now-candidateAt>=0.35;
    }
    void clearCandidate(){candidate.clear();candidateAt=lastCandidate=-1;}
    void clearSuppression(){suppressedFile.clear();suppressedWindow.clear();}
    bool isSuppressed(const Observation &o) {
        const bool same=!suppressedFile.empty()&&!o.file.empty()?sameFile(suppressedFile,o.file):
            !suppressedWindow.empty()&&suppressedWindow==o.window;
        if(!same)return false;
        // Learn late metadata without losing the stopped window or weakening a full path.
        if(suppressedFile.empty()||(suppressedFile.find('/')==std::string::npos&&o.file.find('/')!=std::string::npos))suppressedFile=o.file;
        if(!o.window.empty())suppressedWindow=o.window;
        return true;
    }
    void begin(double now,const Observation &o) {
        clock.start(now,o.seconds);file=o.file;window=o.window;
        manual=held=manualDuration=false;missingAt=-1;clearCandidate();clearSuppression();
    }
    void startManual(double now,int seconds) {
        Observation o;o.seconds=seconds;begin(now,o);manual=true;
    }
    void stop() {
        if(clock.active){suppressedFile=file;suppressedWindow=window;}
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
    Change observe(double now,const Observation &o) {
        if(!monitoring||manual)return Change::Unchanged;
        if(!o.present){
            clearCandidate();clearSuppression();
            if(held||!clock.active)return Change::Unchanged;
            if(missingAt<0)missingAt=now;
            if(now-missingAt>=std::max(0.8,double(grace))){stop();clearSuppression();return Change::Stopped;}
            return Change::Unchanged;
        }
        missingAt=-1;
        if(isSuppressed(o)){clearCandidate();return Change::Unchanged;}
        // Keep suppression until a different show is stable, so one bad sample cannot release it.
        if(o.background&&o.matched&&!o.ambiguous){
            if(held){clearCandidate();return Change::Unchanged;}
            if(!stable("background:"+key(o),now))return Change::Unchanged;
            wasRunning=clock.active&&!clock.paused;clock.pause(now);held=true;clearCandidate();clearSuppression();return Change::Background;
        }
        if(held){
            if(o.file.empty()||o.ambiguous){clearCandidate();return Change::Unchanged;}
            if(!stable("return:"+key(o),now))return Change::Unchanged;
            if(clock.active&&sameFile(file,o.file)) {
                held=false;window=o.window;if(wasRunning)clock.resume(now);clearCandidate();
                return Change::Unchanged;
            }
            begin(now,o);return Change::Started;
        }
        if(!clock.active){
            if(stable("start:"+key(o),now)){begin(now,o);return Change::Started;}
            return Change::Unchanged;
        }
        // Ambiguous rules still identify a new filename and carry the default duration.
        if(o.file.empty()){clearCandidate();return Change::Unchanged;}
        if(file.empty()||sameFile(file,o.file)){
            clearCandidate();window=o.window;
            if(file.empty()||(file.find('/')==std::string::npos&&o.file.find('/')!=std::string::npos)){
                file=o.file;if(!manualDuration)clock.changeDuration(now,o.seconds);
            }
            return Change::Unchanged;
        }
        if(stable("switch:"+key(o),now)){begin(now,o);return Change::Started;}
        return Change::Unchanged;
    }
};
}
