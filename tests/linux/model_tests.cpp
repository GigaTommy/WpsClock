#include "../../apps/linux/model.h"
#include <cstdlib>
#include <iostream>
using namespace wpsclock;
static int checks=0;
static void check(bool ok,const char *label){if(!ok){std::cerr<<"FAIL: "<<label<<'\n';std::exit(1);}++checks;}
static Observation obs(const std::string &file,int seconds=120,bool background=false) {
    Observation o;o.present=true;o.file=file;o.window="100";o.seconds=seconds;o.matched=true;o.background=background;return o;
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
    std::cout<<"PASS "<<checks<<" model checks\n";
}
