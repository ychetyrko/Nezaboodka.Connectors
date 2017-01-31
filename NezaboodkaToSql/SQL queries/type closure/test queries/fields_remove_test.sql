/*======================================
		Remove fields tests
 (based on `Add types and fields tests`)
======================================*/

USE `nz_test_closure`;

/*---------------------------------------/
	Remove single nested field
		without Back Reference
--------------------------------------*/
call before_alter_fields();
insert into `field_rem_list`
(`owner_type_name`, `name`)
VALUES
('User', 'Login');
call remove_fields();

/*---------------------------------------/
	Remove nonexisting single field
--------------------------------------*/
call before_alter_fields();
insert into `field_rem_list`
(`owner_type_name`, `name`)
VALUES
('Admin', 'Login');
call remove_fields();

/*---------------------------------------/
	Remove single nested field
		with Back Reference
--------------------------------------*/
call before_alter_fields();
insert into `field_rem_list`
(`owner_type_name`, `name`)
VALUES
('User', 'Group');
call remove_fields();

/*---------------------------------------/
	Remove single non-nested field
		with Back Reference
--------------------------------------*/
call before_alter_fields();
insert into `field_rem_list`
(`owner_type_name`, `name`)
VALUES
('Group', 'Admin');
call remove_fields();

/*---------------------------------------/
	Remove multiple non-nested fields
		with(out) Back Reference
--------------------------------------*/
call before_alter_fields();
insert into `field_rem_list`
(`owner_type_name`, `name`)
VALUES
('Admin', 'ControlGroup'),
('Group', 'Title'),
('UberAdmin', 'UberRating');
call remove_fields();
