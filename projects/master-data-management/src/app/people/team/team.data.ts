import { TeamRecord } from './team.model';

export function blankTeam(): TeamRecord {
  return {
    code: '',
    name: '',
    department: '',
    leaderId: '',
    description: '',
    status: 'Active',
    members: [],
    shiftCode: '',
    responsibilities: [],
    notifyLeader: true,
    autoAssign: false,
    createdOn: '',
    updatedOn: ''
  };
}
