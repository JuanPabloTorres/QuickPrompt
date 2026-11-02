"""Optional application-neutral observation; never an authorization or result port."""
from datetime import datetime, timezone
import inspect
import json
import os
from pathlib import Path
from uuid import uuid4


class ObservationPort:
    def __init__(self, sink, request_id):
        self.sink, self.request_id, self.errors = sink, request_id, 0

    def emit(self, kind, phase, status, *, code=None, observed=None):
        if self.sink is None:
            return
        frame=inspect.currentframe().f_back
        event={'schema':'adaptive-observation/1','id':uuid4().hex,
            'requestId':self.request_id,'observedAt':datetime.now(timezone.utc).isoformat(),
            'kind':kind,'phase':phase,'status':status,'code':code,
            'location':{'file':str(Path(frame.f_code.co_filename).resolve()),'line':frame.f_lineno,
                        'symbol':frame.f_code.co_name,'kind':'observed_checkpoint'},
            'observed':observed}
        del frame
        try:
            self.sink(event)
        except Exception:
            self.errors+=1  # Observation cannot grant authority or alter inference.


class JsonlObservationSink:
    """Explicit host-selected local sink, metadata only; stdout protocol stays clean."""
    def __init__(self,path,campaign,case_id):
        self.path=Path(path);self.campaign=campaign;self.case_id=case_id
        if not self.path.is_absolute():raise ValueError('Observation path must be absolute')

    def __call__(self,event):
        self.path.parent.mkdir(parents=True,exist_ok=True)
        if self.path.exists() and self.path.stat().st_size>16_000_000:raise OSError('Observation budget')
        raw=(json.dumps({**event,'campaignId':self.campaign,'caseId':self.case_id,'runId':self.case_id},ensure_ascii=False)+'\n').encode('utf-8')
        fd=os.open(self.path,os.O_WRONLY|os.O_CREAT|os.O_APPEND,0o600)
        try:
            if os.write(fd,raw)!=len(raw):raise OSError('Incomplete observation write')
        finally:os.close(fd)
