#-----------------------------------------
#		Types ordering tests
#-----------------------------------------
#	Types between brackets may have any order
#-----------------------------------------
use `nz_test_closure`;
# Reverse order
call before_alter_types();
INSERT INTO `type_add_list`
(`name`, `table_name`, `base_type_name`)
VALUES
('MegaAdmin', '_mega_admin', 'Admin'),
('Admin', '_admin', 'User'),
('User', '_user', NULL),
('Vehicle', '_vehicle', NULL);
call add_all_types();
# expected: (User, Vehicle), Admin, MegaAdmin
#-----------------------------------------
# Mixed order
call before_alter_types();
INSERT INTO `type_add_list`
(`name`, `table_name`, `base_type_name`)
VALUES
('HotRod', '_hot_rod', 'Car'),
('Chopper', '_chopper', 'Motocycle'),
('Car', '_car', 'Vehicle'),
('Motocycle', '_motocycle', 'Vehicle'),
('Group', '_group', NULL),
('Sedan', '_sedan', 'Car');
call add_all_types();
# expected: Group, (Car, Motocycle), (Chopper, HotRod, Sedan)
