use `nz_test_closure`;

truncate table `type_add_list`;

INSERT INTO `type_add_list`
(`name`, `table_name`, `base_type_name`)
VALUES
('User', '_user', NULL),
('Admin', '_admin', 'User'),
('Moderator', '_moderator', 'User'),
('UberAdmin', '_uber_admin', 'Admin'),
('Group', '_group', NULL);

select *
from `type_add_list`;


call `add_all_types`;

select * from `new_type`;
#call `remove_type`('Moderator');
