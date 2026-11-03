#include "../../apps/linux/model.h"
#include <cstdlib>
#include <iostream>
using namespace wpsclock;
static int checks=0;
static void check(bool ok,const char *label){if(!ok){std::cerr<<"FAIL: "<<label<<'\n';std::exit(1);}++checks;}
static Observation obs(const std::string &file,int seconds=120,bool background=false) {
    Observation o;o.present=true;o.file=file;o.window="100";o.seconds=seconds;o.matched=true;o.background=background;return o;
}
static void ambiguousFileSwitch(){
    for(const std::string window:{"100","200"}){
        Session s;auto a=obs("A.pptx"),b=obs("B.pptx",480);b.window=window;b.matched=false;b.ambiguous=true;
        s.observe(0,a);s.observe(.4,a);s.choose(1,600);s.clock.pause(2);
        s.observe(3,b);check(s.file=="A.pptx"&&s.clock.duration==600,"one ambiguous new-file sample does not switch");
        s.observe(3.4,a);s.observe(4,b);auto change=s.observe(4.4,b);
        check(change==Session::Change::Started&&s.file=="B.pptx"&&s.window==window&&s.clock.duration==480,"stable ambiguous new file uses fallback duration");
        check(s.clock.elapsed(4.4)==0&&!s.clock.paused&&!s.manualDuration,"ambiguous new file starts a fresh session");
        s.observe(4.8,b);check(std::abs(s.clock.elapsed(4.8)-.4)<1e-9,"same ambiguous file does not restart repeatedly");
        auto unnamed=b;unnamed.file.clear();s.observe(5.2,unnamed);
        check(s.file=="B.pptx"&&s.clock.duration==480,"ambiguity without a filename preserves the current session");
    }
}
static void stoppedIdentity(){
    for(const std::string initial:{"","A.pptx","/talks/A.pptx"}){
        Session s;auto first=obs(initial),a=obs("A.pptx"),full=obs("/talks/A.pptx"),b=obs("B.pptx",300);
        auto unnamed=obs("");unnamed.matched=false;
        s.observe(0,first);s.observe(.4,first);s.stop();
        s.observe(1,unnamed);s.observe(1.4,a);s.observe(1.8,a);
        check(!s.clock.active,"stopped window stays stopped when its filename arrives or disappears");
        s.observe(2.2,full);s.observe(2.6,full);
        check(!s.clock.active,"full-path refinement does not restart a stopped session");
        s.stop();s.observe(3,full);s.observe(3.4,full);
        check(!s.clock.active,"ending an already stopped session preserves suppression");
        s.observe(4,b);s.observe(4.4,full);s.observe(4.8,full);
        check(!s.clock.active,"one different-file sample does not release stop suppression");
        s.observe(5.2,b);s.observe(5.6,b);
        check(s.clock.active&&s.file=="B.pptx"&&s.clock.duration==300&&s.clock.elapsed(5.6)==0,"stable different file restarts even when the window is reused");
        s.stop();s.observe(6,Observation());s.observe(6.4,b);s.observe(6.8,b);
        check(s.clock.active&&s.clock.elapsed(6.8)==0,"exit and reentry release stop suppression");
    }
    Session s;auto unnamed=obs("");s.observe(0,unnamed);s.observe(.4,unnamed);s.stop();
    auto other=obs("B.pptx",300);other.window="200";s.observe(1,other);s.observe(1.4,other);
    check(s.clock.active&&s.window=="200"&&s.clock.elapsed(1.4)==0,"different window can start after stopping an unidentified show");
    s.stop();s.startManual(2,90);s.stop();s.observe(3,other);s.observe(3.4,other);
    check(s.clock.active&&s.file=="B.pptx","manual start clears earlier automatic suppression");
    s.stop();auto full=other;full.file="/talks/B.pptx";full.window="300";s.observe(4,full);s.observe(4.4,full);
    check(!s.clock.active,"same file in a replacement window remains suppressed");
    auto different=full;different.file="/other/B.pptx";s.observe(5,different);s.observe(5.4,different);
    check(s.clock.active&&s.file==different.file,"different full paths with the same basename can start a new session");
}
static void sessionChanges(){
    using Change=Session::Change;
    Session s;auto a=obs("A.pptx"),b=obs("B.pptx",300),bg=obs("Theme.pptx",0,true);
    check(s.observe(0,a)==Change::Unchanged&&s.observe(.4,a)==Change::Started,"automatic start reports one session boundary");
    check(s.observe(.8,a)==Change::Unchanged,"ordinary observation does not retire an alert");
    check(s.observe(1.2,b)==Change::Unchanged&&s.observe(1.6,b)==Change::Started,"stable file switch reports a new session");
    check(s.observe(2,bg)==Change::Unchanged&&s.observe(2.4,bg)==Change::Background,"stable background hold retires previous alerts");
    check(s.observe(2.8,bg)==Change::Unchanged,"remaining on background does not repeat the boundary");
    check(s.observe(3.2,b)==Change::Unchanged&&s.observe(3.6,b)==Change::Unchanged&&!s.held,"return to same speech resumes without a new session");
    check(s.observe(4,Observation())==Change::Unchanged&&s.observe(6.9,Observation())==Change::Unchanged,"exit grace does not retire alerts prematurely");
    check(s.observe(7,Observation())==Change::Stopped&&!s.clock.active,"automatic exit retires alerts at the grace deadline");
    check(s.observe(7.4,Observation())==Change::Unchanged,"idle scanning does not repeat the stop event");
    s.observe(8,a);s.observe(8.4,a);s.stop();
    check(s.observe(8.8,a)==Change::Unchanged,"suppressed session does not emit a spurious start");
    s.startManual(9,120);check(s.observe(9.4,bg)==Change::Unchanged&&!s.held,"manual session ignores automatic boundary events");
    s=Session();auto unnamed=obs("");unnamed.matched=false;s.observe(10,unnamed);s.observe(10.4,unnamed);
    check(s.observe(11,a)==Change::Unchanged&&s.file=="A.pptx"&&s.clock.elapsed(11)>0,"late filename keeps the same session and alert");
    auto full=a;full.file="/talks/A.pptx";
    check(s.observe(11.4,full)==Change::Unchanged,"path refinement does not become a new session");
    s.monitoring=false;check(s.observe(12,b)==Change::Unchanged,"disabled monitoring does not emit session changes");s.monitoring=true;
    s.observe(13,bg);s.observe(13.4,bg);
    check(s.observe(14,b)==Change::Unchanged&&s.observe(14.4,b)==Change::Started,"different speech after background reports a new session");
}
int main(){
    Clock c;c.start(100,60);
    check(c.display(100.01,false)=="01:00","no premature first second");
    check(c.display(101,false)=="00:59","elapsed seconds");
    check(c.advance(149.99,10,false)==0,"warning boundary before");
    check(c.advance(150,10,false)==1,"warning once");
    check(c.advance(151,10,false)==0,"no duplicate warning");
    c.pause(151);check(c.display(900,false)=="00:09","pause across long gap");
    c.resume(1000);check(c.advance(1009,10,false)==2,"resumed final alarm");
    check(c.display(1010,false,"over ")=="over 00:01","overtime");
    check(c.advance(1100,10,false)==0,"final once");
    c.start(0,60);check(c.advance(100,10,true)==2,"delayed tick final takes priority");
    check(c.display(1000,false)=="00:00"&&c.paused,"freeze at deadline");
    c.changeDuration(1000,120);check(!c.paused&&c.display(1000,false)=="01:00","extend frozen duration");
    c.start(0,0);check(c.display(200,false)=="03:20"&&c.advance(200,60,true)==0,"unlimited");
    c.start(5,60);check(c.display(4,false)=="01:00","clock cannot move below zero");
    check(Clock::format(3600)=="60:00","long duration");
    check(!sameFile("/a/Report.pptx","/b/Report.pptx"),"different full paths");
    check(!sameFile("Report.pptx","report.pptx"),"Linux case sensitivity");
    check(sameFile("/a/Report.pptx","Report.pptx"),"name refinement");
    Session s;auto a=obs("A.pptx"),b=obs("B.pptx",300),bg=obs("Theme.pptx",0,true);
    s.observe(0,a);check(!s.clock.active,"debounce start");s.observe(.4,a);
    check(s.clock.active&&s.clock.duration==120,"stable start");
    s.observe(20,a);check(s.clock.elapsed(20)==19.6,"normal slides never restart");
    s.observe(21,b);s.observe(21.1,a);check(s.file=="A.pptx","one bad observation ignored");
    s.observe(22,b);s.observe(22.4,b);check(s.file=="B.pptx"&&s.clock.elapsed(22.4)==0,"stable new file");
    s.observe(30,bg);s.observe(30.4,bg);double used=s.clock.elapsed(30.4);
    check(s.held&&s.clock.paused,"background pauses");
    s.observe(900,Observation());check(s.held&&s.clock.elapsed(900)==used,"missing background does not resume");
    auto unknown=b;unknown.ambiguous=true;s.observe(901,unknown);check(s.held,"ambiguous return does not resume");
    s.observe(902,b);s.observe(902.4,b);check(!s.held&&!s.clock.paused&&s.clock.elapsed(902.4)==used,"same speech resumes remaining");
    s.clock.pause(910);s.observe(911,bg);s.observe(911.4,bg);s.observe(920,b);s.observe(920.4,b);
    check(s.clock.paused,"manual pause survives background");
    s.observe(921,bg);s.observe(921.4,bg);s.observe(922,a);s.observe(922.4,a);
    check(s.file=="A.pptx"&&!s.clock.paused&&s.clock.duration==120,"different speech after background starts");
    s.observe(930,Observation());s.observe(932,a);check(s.clock.active,"grace interval");
    s.observe(933,Observation());s.observe(937,Observation());check(!s.clock.active,"exit after grace");
    s.observe(940,a);s.observe(940.4,a);s.stop();s.observe(941,a);s.observe(942,a);check(!s.clock.active,"explicit stop not immediately restarted");
    s.observe(943,Observation());s.observe(944,a);s.observe(944.4,a);check(s.clock.active,"new show after explicit stop");
    s.startManual(950,600);s.observe(960,bg);check(!s.held&&s.clock.duration==600,"manual ignores automatic detection");
    s=Session();auto noName=a;noName.file="";noName.seconds=480;noName.matched=false;
    s.observe(0,noName);s.observe(.4,noName);s.observe(10,a);
    check(s.clock.duration==120&&s.clock.elapsed(10)==9.6,"late identity preserves elapsed");
    s.choose(11,600);s.observe(12,a);check(s.clock.duration==600,"manual duration survives updates");
    s.observe(20,bg);s.observe(20.4,bg);s.monitoring=false;s.observe(21,a);check(s.held,"disabled monitoring retains hold");
    s.monitoring=true;s.observe(22,a);s.observe(22.4,a);check(!s.held&&s.clock.duration==600,"monitoring reenabled preserves context");
    s.observe(30,bg);s.observe(30.4,bg);s.observe(40,b);s.observe(45,b);check(s.held,"long sampling gap restarts debounce");
    s.observe(45.4,b);check(!s.held&&s.file=="B.pptx","continuous samples exit background");
    s.observe(46,bg);s.observe(46.4,bg);s.toggle(47,480);check(!s.held&&s.manual&&!s.clock.paused,"manual takeover from background");
    s.startManual(50,120);s.reset(55,300);check(s.clock.duration==300&&s.clock.elapsed(55)==0,"reset applies updated default");
    s.choose(56,600);s.reset(57,300);check(s.clock.duration==600,"reset preserves explicit session duration");
    ambiguousFileSwitch();stoppedIdentity();sessionChanges();
    std::cout<<"PASS "<<checks<<" model checks\n";
}
